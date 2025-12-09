from agents.explorer.manual_reader import ManualReader
from storage.vector_store import InMemoryVectorStore


def test_manual_reader_uses_custom_embed_fn():
    calls = []

    def fake_embed(text: str):
        calls.append(text)
        return [1.0, 0.0]

    reader = ManualReader(vector_store=InMemoryVectorStore(), embed_fn=fake_embed)
    reader.index_chunks(["hello world"])
    assert calls, "custom embed fn should be invoked"

