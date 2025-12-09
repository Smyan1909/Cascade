import asyncio
from unittest.mock import AsyncMock, MagicMock

import pytest

from cascade_client.a2a import AgentA2AClient, InMemoryDeduper
from cascade_client.auth.context import CascadeContext
from cascade_client.models import AgentEnvelope, AgentMessage, AgentRegisterResponse


def test_agent_message_round_trip():
    msg = AgentMessage(
        message_id="m1",
        user_id="u1",
        app_id="a1",
        sender_agent_id="s1",
        sender_role="worker",
        target_role="explorer",
        run_id="r1",
        headers={"k": "v"},
        json_payload='{"ok": true}',
        created_at_ms=123,
    )

    proto = msg.to_proto()
    back = AgentMessage.from_proto(proto)

    assert back.message_id == msg.message_id
    assert back.headers == {"k": "v"}
    assert back.created_at_ms == 123


def test_in_memory_deduper():
    deduper = InMemoryDeduper()
    assert not deduper.is_processed("m1")
    deduper.mark_processed("m1")
    assert deduper.is_processed("m1")


@pytest.mark.asyncio
async def test_a2a_register_and_send(monkeypatch):
    ctx = CascadeContext(user_id="u1", app_id="a1", auth_token="t1")
    grpc_client = MagicMock()
    grpc_client.register_agent_async = AsyncMock(
        return_value=AgentRegisterResponse(agent_id="agent-1")
    )
    grpc_client.send_agent_message_async = AsyncMock()

    client = AgentA2AClient(ctx, grpc_client, role="worker")

    await client.ensure_registered()
    await client.send({"ping": "pong"}, target_role="explorer")

    grpc_client.register_agent_async.assert_awaited_once()
    grpc_client.send_agent_message_async.assert_awaited()


@pytest.mark.asyncio
async def test_a2a_listen_skips_duplicates(monkeypatch):
    ctx = CascadeContext(user_id="u1", app_id="a1", auth_token="t1")
    grpc_client = MagicMock()
    grpc_client.register_agent_async = AsyncMock(
        return_value=AgentRegisterResponse(agent_id="agent-1")
    )

    msg = AgentMessage(
        message_id="dup",
        user_id="u1",
        app_id="a1",
        sender_agent_id="s1",
        json_payload="{}",
    )
    env = AgentEnvelope(message=msg, ack_token="ack1")

    async def _stream():
        yield env
        yield env  # duplicate delivery

    grpc_client.stream_agent_inbox_async = MagicMock(return_value=_stream())
    grpc_client.ack_agent_message_async = AsyncMock()

    handled = []

    async def handler(m):
        handled.append(m.message_id)

    client = AgentA2AClient(ctx, grpc_client, role="worker", deduper=InMemoryDeduper())

    # Run listener briefly
    async def _run():
        task = asyncio.create_task(client.listen(handler))
        await asyncio.sleep(0)  # allow iteration
        await client.stop()
        await task

    await _run()

    # Handler called once despite duplicate delivery
    assert handled == ["dup"]
    grpc_client.ack_agent_message_async.assert_awaited()

