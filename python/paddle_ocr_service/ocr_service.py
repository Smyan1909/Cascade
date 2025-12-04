from concurrent import futures
import logging
from typing import Iterable

import grpc

from protos import vision_pb2, vision_pb2_grpc
from .models import ModelRegistry


class PaddleOcrServicer(vision_pb2_grpc.PaddleOcrServiceServicer):
    """Stub implementation that returns deterministic responses for integration tests."""

    def __init__(self) -> None:
        self._registry = ModelRegistry()
        self._registry.load()

    def Recognize(self, request, context):
        logging.info("Received stub PaddleOCR request (%d bytes)", len(request.image_data))
        response = vision_pb2.PaddleOcrResponse(
            success=True,
            full_text="",
            confidence=0.0,
            model_used=self._registry.config.model_name,
            processing_time_ms=1,
        )
        return response

    def RecognizeBatch(self, request_iterator: Iterable[vision_pb2.PaddleOcrRequest], context):
        for request in request_iterator:
            yield self.Recognize(request, context)

    def GetStatus(self, request, context):
        return vision_pb2.PaddleOcrStatus(
            is_ready=True,
            model_loaded=self._registry.config.model_name,
            gpu_available=self._registry.config.use_gpu,
            gpu_memory_used_mb=0,
        )


def serve(address: str = "0.0.0.0:50052") -> None:
    server = grpc.server(futures.ThreadPoolExecutor(max_workers=2))
    vision_pb2_grpc.add_PaddleOcrServiceServicer_to_server(PaddleOcrServicer(), server)
    server.add_insecure_port(address)
    server.start()
    logging.info("PaddleOCR stub server listening on %s", address)
    server.wait_for_termination()


if __name__ == "__main__":
    logging.basicConfig(level=logging.INFO)
    serve()


