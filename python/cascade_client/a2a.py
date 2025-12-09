"""
Agent-to-Agent (A2A) helper for Cascade agents.

Provides a small SDK for registering agents, sending messages, and streaming an
inbox with at-least-once delivery and idempotent handlers. Uses a pluggable
deduper so handlers can remain idempotent; defaults to in-memory but can persist
via Firestore when available.
"""

from __future__ import annotations

import asyncio
import time
import uuid
from typing import Awaitable, Callable, Optional, Protocol, Set

from cascade_client.auth.context import CascadeContext
from cascade_client.grpc_client import CascadeGrpcClient
from cascade_client.models import (
    AgentAck,
    AgentEnvelope,
    AgentInboxRequest,
    AgentMessage,
    AgentRegisterRequest,
)


class MessageDeduper(Protocol):
    """Interface for idempotent message handling."""

    def is_processed(self, message_id: str) -> bool:
        ...

    def mark_processed(self, message_id: str) -> None:
        ...


class InMemoryDeduper(MessageDeduper):
    """Simple in-memory deduper."""

    def __init__(self):
        self._seen: Set[str] = set()

    def is_processed(self, message_id: str) -> bool:
        return message_id in self._seen

    def mark_processed(self, message_id: str) -> None:
        self._seen.add(message_id)


class FirestoreDeduper(MessageDeduper):
    """
    Firestore-backed deduper keyed by agent_id/message_id.

    If Firestore is unavailable, falls back to in-memory to avoid blocking usage.
    """

    def __init__(self, context: CascadeContext, agent_id: str):
        self._context = context
        self._agent_id = agent_id
        try:
            from storage.firestore_client import FirestoreClient

            self._fs: Optional[FirestoreClient] = FirestoreClient(context)
        except Exception:
            self._fs = None
        self._memory = InMemoryDeduper()

    def _doc_path(self, message_id: str) -> str:
        return (
            f"{self._context.get_firestore_path_prefix()}/a2a_processed/"
            f"{self._agent_id}/messages/{message_id}"
        )

    def is_processed(self, message_id: str) -> bool:
        if self._memory.is_processed(message_id):
            return True
        if self._fs:
            doc = self._fs.get_document(self._doc_path(message_id))
            if doc:
                self._memory.mark_processed(message_id)
                return True
        return False

    def mark_processed(self, message_id: str) -> None:
        self._memory.mark_processed(message_id)
        if self._fs:
            try:
                self._fs.upsert_document(
                    self._doc_path(message_id),
                    {"message_id": message_id, "ack_time_ms": int(time.time() * 1000)},
                )
            except Exception:
                # Swallow persistence errors to avoid breaking message handling.
                pass


class AgentA2AClient:
    """Convenience wrapper around AgentCommService with idempotent handlers."""

    def __init__(
        self,
        context: CascadeContext,
        grpc_client: CascadeGrpcClient,
        role: str,
        run_id: Optional[str] = None,
        deduper: Optional[MessageDeduper] = None,
    ):
        self._context = context
        self._grpc = grpc_client
        self._role = role
        self._run_id = run_id
        self._agent_id: Optional[str] = None
        self._deduper = deduper
        self._stop_event = asyncio.Event()

    @property
    def agent_id(self) -> Optional[str]:
        return self._agent_id

    async def ensure_registered(self) -> str:
        """Register the agent if not already registered."""
        if self._agent_id:
            return self._agent_id

        request = AgentRegisterRequest(
            user_id=self._context.user_id,
            app_id=self._context.app_id,
            auth_token=self._context.auth_token,
            role=self._role,
            run_id=self._run_id,
        )
        response = await self._grpc.register_agent_async(request)
        self._agent_id = response.agent_id

        # Backfill deduper now that we have an agent_id
        if self._deduper is None:
            self._deduper = FirestoreDeduper(self._context, self._agent_id)
        return self._agent_id

    async def send(
        self,
        payload: dict,
        *,
        target_agent_id: Optional[str] = None,
        target_role: Optional[str] = None,
        run_id: Optional[str] = None,
        headers: Optional[dict] = None,
    ) -> None:
        """Send a JSON payload to a target agent/role."""
        agent_id = await self.ensure_registered()
        msg = AgentMessage(
            message_id=str(uuid.uuid4()),
            user_id=self._context.user_id,
            app_id=self._context.app_id,
            sender_agent_id=agent_id,
            sender_role=self._role,
            target_agent_id=target_agent_id,
            target_role=target_role,
            run_id=run_id or self._run_id,
            headers=headers or {},
            json_payload=json_dumps_safe(payload),
            created_at_ms=int(time.time() * 1000),
        )
        await self._grpc.send_agent_message_async(msg)

    async def listen(
        self,
        handler: Callable[[AgentMessage], Awaitable[None]],
    ):
        """
        Stream inbox messages and invoke handler for each message.

        Handler must be idempotent; deduper ensures duplicate deliveries are
        skipped before invoking the handler. Acks are sent after handler success.
        """
        agent_id = await self.ensure_registered()
        inbox_req = AgentInboxRequest(
            agent_id=agent_id,
            user_id=self._context.user_id,
            app_id=self._context.app_id,
            run_id=self._run_id,
            role=self._role,
        )

        deduper = self._deduper or InMemoryDeduper()

        async for envelope in self._grpc.stream_agent_inbox_async(inbox_req):
            if self._stop_event.is_set():
                break

            message = envelope.message
            if deduper.is_processed(message.message_id):
                await self._ack(envelope)
                continue

            try:
                await handler(message)
                deduper.mark_processed(message.message_id)
                await self._ack(envelope)
            except Exception:
                # Do not ack on failure; message will be retried (at-least-once).
                continue

    async def stop(self):
        """Signal the inbox stream to stop."""
        self._stop_event.set()

    async def _ack(self, envelope: AgentEnvelope) -> None:
        ack = AgentAck(
            message_id=envelope.message.message_id,
            ack_token=envelope.ack_token,
            agent_id=self._agent_id or "",
            user_id=self._context.user_id,
            app_id=self._context.app_id,
        )
        await self._grpc.ack_agent_message_async(ack)


def json_dumps_safe(payload: dict) -> str:
    """Serialize payload to JSON without raising."""
    import json

    try:
        return json.dumps(payload)
    except Exception:
        return json.dumps({"_error": "failed to serialize", "repr": repr(payload)})

