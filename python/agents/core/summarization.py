"""Conversation summarization utilities."""

from __future__ import annotations

from typing import Dict, List, Optional, Union

from clients.llm_client import LlmClient, LlmMessage, load_summarization_client_from_env


# Module-level cache for the summarization client (lazy initialization)
_summarization_client: Optional[LlmClient] = None


def _get_summarization_client() -> LlmClient:
    """Get or initialize the summarization client (lazy initialization with caching)."""
    global _summarization_client
    if _summarization_client is None:
        _summarization_client = load_summarization_client_from_env()
    return _summarization_client


def summarize_conversation(
    conversation: Union[List[LlmMessage], List[Dict[str, str]]],
    *,
    max_tokens: Optional[int] = None,
) -> str:
    """
    Summarize a conversation using a lightweight LLM model.
    
    Args:
        conversation: List of messages in either format:
            - List[LlmMessage]: List of LlmMessage objects
            - List[Dict[str, str]]: List of dicts with 'role' and 'content' keys
        max_tokens: Optional maximum tokens for the summary. If None, uses model default.
    
    Returns:
        A string containing the conversation summary.
    
    Raises:
        ValueError: If conversation is empty or invalid format.
        RuntimeError: If summarization client fails to initialize or generate.
    """
    if not conversation:
        raise ValueError("Conversation cannot be empty")
    
    # Convert conversation to LlmMessage format
    messages: List[LlmMessage] = []
    for msg in conversation:
        if isinstance(msg, LlmMessage):
            messages.append(msg)
        elif isinstance(msg, dict):
            role = msg.get("role", "user")
            content = msg.get("content", "")
            if not content:
                continue  # Skip empty messages
            messages.append(LlmMessage(role=role, content=content))
        else:
            raise ValueError(f"Invalid message format: expected LlmMessage or dict, got {type(msg)}")
    
    if not messages:
        raise ValueError("Conversation contains no valid messages")
    
    # Format conversation for summarization
    conversation_text = "\n".join(
        f"{msg.role.upper()}: {msg.content}" for msg in messages
    )
    
    # Create summarization prompt
    system_prompt = (
        "You are a helpful assistant that creates concise, informative summaries of conversations. "
        "Your summaries should capture the key points, decisions, and outcomes of the conversation "
        "in a clear and structured manner. Focus on the most important information and maintain "
        "context for future interactions."
        "Try to keep the summary under 1000 tokens."
    )
    
    user_prompt = f"Please summarize the following conversation:\n\n{conversation_text}"
    
    # Prepare messages for the LLM
    llm_messages = [
        LlmMessage(role="system", content=system_prompt),
        LlmMessage(role="user", content=user_prompt),
    ]
    
    # Get summarization client and generate summary
    try:
        client = _get_summarization_client()
        response = client.generate(
            messages=llm_messages,
            temperature=0.1,  # Lower temperature for more consistent summaries
            max_tokens=max_tokens,
        )
        return response.content.strip()
    except Exception as e:
        raise RuntimeError(f"Failed to generate conversation summary: {e}") from e

