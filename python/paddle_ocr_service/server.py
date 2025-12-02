"""
PaddleOCR gRPC Server

This module provides the gRPC server that hosts the PaddleOCR service.
It can be run as a standalone service or integrated with the Cascade system.

Usage:
    python -m paddle_ocr_service.server --port 50052
    python -m paddle_ocr_service.server --port 50052 --model ppocrv4 --gpu
"""

import argparse
import logging
import signal
import sys
import struct
from concurrent import futures
from typing import Iterator

import grpc

from .ocr_service import PaddleOcrService, OcrResult

logger = logging.getLogger(__name__)


class PaddleOcrServicer:
    """
    gRPC servicer for PaddleOCR.
    
    Implements the PaddleOcrService defined in vision.proto.
    """
    
    def __init__(self, service: PaddleOcrService):
        """
        Initialize the servicer.
        
        Args:
            service: The PaddleOcrService instance to use
        """
        self.service = service
    
    def Recognize(self, request_data: bytes, context) -> bytes:
        """
        Handle a Recognize RPC call.
        
        Args:
            request_data: Serialized request bytes
            context: gRPC context
            
        Returns:
            Serialized response bytes
        """
        try:
            # Parse request
            request = self._parse_request(request_data)
            
            # Call service
            result = self.service.recognize(
                image_data=request["image_data"],
                use_angle_cls=request.get("use_angle_classifier", True),
                detect_only=request.get("detect_only", False)
            )
            
            # Serialize response
            return self._serialize_response(result)
            
        except Exception as e:
            logger.error(f"Error in Recognize: {e}")
            error_result = OcrResult(
                success=False,
                error_message=str(e)
            )
            return self._serialize_response(error_result)
    
    def RecognizeBatch(
        self, 
        request_iterator: Iterator[bytes],
        context
    ) -> Iterator[bytes]:
        """
        Handle a RecognizeBatch streaming RPC call.
        
        Args:
            request_iterator: Iterator of serialized request bytes
            context: gRPC context
            
        Yields:
            Serialized response bytes
        """
        for request_data in request_iterator:
            yield self.Recognize(request_data, context)
    
    def GetStatus(self, request_data: bytes, context) -> bytes:
        """
        Handle a GetStatus RPC call.
        
        Args:
            request_data: Serialized request bytes (empty)
            context: gRPC context
            
        Returns:
            Serialized status response bytes
        """
        status = self.service.get_status()
        return self._serialize_status(status)
    
    def _parse_request(self, data: bytes) -> dict:
        """Parse binary request data."""
        offset = 0
        
        # Read image data length and data
        image_len = struct.unpack_from(">I", data, offset)[0]
        offset += 4
        image_data = data[offset:offset + image_len]
        offset += image_len
        
        # Read language string
        lang_len = struct.unpack_from(">H", data, offset)[0]
        offset += 2
        language = data[offset:offset + lang_len].decode("utf-8")
        offset += lang_len
        
        # Read model type
        model_type = struct.unpack_from(">I", data, offset)[0]
        offset += 4
        
        # Read flags
        use_angle_classifier = struct.unpack_from("?", data, offset)[0]
        offset += 1
        
        return {
            "image_data": image_data,
            "language": language,
            "model_type": model_type,
            "use_angle_classifier": use_angle_classifier,
            "detect_only": False
        }
    
    def _serialize_response(self, result: OcrResult) -> bytes:
        """Serialize OcrResult to binary response data."""
        parts = []
        
        # Success flag (1 byte)
        parts.append(struct.pack("?", result.success))
        
        if not result.success:
            # Error message (2 bytes length + utf-8 data)
            error_bytes = result.error_message.encode("utf-8")
            parts.append(struct.pack(">H", len(error_bytes)))
            parts.append(error_bytes)
            return b"".join(parts)
        
        # Full text (2 bytes length + utf-8 data) - truncate if > 65535 bytes
        text_bytes = result.full_text.encode("utf-8")[:65535]
        parts.append(struct.pack(">H", len(text_bytes)))
        parts.append(text_bytes)
        
        # Confidence (8 bytes double)
        parts.append(struct.pack(">d", result.confidence))
        
        # Processing time (4 bytes int)
        parts.append(struct.pack(">i", result.processing_time_ms))
        
        # Model used (2 bytes length + utf-8 data)
        model_bytes = result.model_used.encode("utf-8")[:65535]
        parts.append(struct.pack(">H", len(model_bytes)))
        parts.append(model_bytes)
        
        # Blocks count (4 bytes int)
        parts.append(struct.pack(">i", len(result.blocks)))
        
        for block in result.blocks:
            # Block text (2 bytes length + utf-8 data)
            block_text = block.text.encode("utf-8")[:65535]
            parts.append(struct.pack(">H", len(block_text)))
            parts.append(block_text)
            
            # Block confidence (8 bytes double)
            parts.append(struct.pack(">d", block.confidence))
            
            # Block bounding box (4 x 4 bytes signed ints)
            bbox = block.bounding_box
            parts.append(struct.pack(">iiii", bbox[0], bbox[1], bbox[2], bbox[3]))
            
            # Words count (4 bytes int)
            parts.append(struct.pack(">i", len(block.words)))
            
            for word in block.words:
                # Word text (2 bytes length + utf-8 data)
                word_text = word.text.encode("utf-8")[:65535]
                parts.append(struct.pack(">H", len(word_text)))
                parts.append(word_text)
                
                # Word confidence (8 bytes double)
                parts.append(struct.pack(">d", word.confidence))
                
                # Word bounding box (4 x 4 bytes signed ints)
                wbbox = word.bounding_box
                parts.append(struct.pack(">iiii", wbbox[0], wbbox[1], wbbox[2], wbbox[3]))
        
        return b"".join(parts)
    
    def _serialize_status(self, status: dict) -> bytes:
        """Serialize status to binary data."""
        parts = []
        
        # is_ready
        parts.append(struct.pack("?", status["is_ready"]))
        
        # model_loaded
        model_bytes = status["model_loaded"].encode("utf-8")
        parts.append(struct.pack(">H", len(model_bytes)))
        parts.append(model_bytes)
        
        # gpu_available
        parts.append(struct.pack("?", status["gpu_available"]))
        
        # gpu_memory_used_mb
        parts.append(struct.pack(">I", status["gpu_memory_used_mb"]))
        
        # supported_languages
        parts.append(struct.pack(">H", len(status["supported_languages"])))
        for lang in status["supported_languages"]:
            lang_bytes = lang.encode("utf-8")
            parts.append(struct.pack(">H", len(lang_bytes)))
            parts.append(lang_bytes)
        
        # paddle_version
        version_bytes = status["paddle_version"].encode("utf-8")
        parts.append(struct.pack(">H", len(version_bytes)))
        parts.append(version_bytes)
        
        # paddleocr_version
        ocr_version_bytes = status["paddleocr_version"].encode("utf-8")
        parts.append(struct.pack(">H", len(ocr_version_bytes)))
        parts.append(ocr_version_bytes)
        
        return b"".join(parts)


def create_server(
    port: int,
    model: str = "ppocrv4",
    language: str = "en",
    use_gpu: bool = False,
    gpu_mem: int = 500,
    max_workers: int = 4
) -> grpc.Server:
    """
    Create and configure the gRPC server.
    
    Args:
        port: Port to listen on
        model: PaddleOCR model to use
        language: Language for recognition
        use_gpu: Whether to use GPU
        gpu_mem: GPU memory limit in MB
        max_workers: Maximum number of worker threads
        
    Returns:
        Configured gRPC server
    """
    print("  Creating PaddleOCR service...", flush=True)
    # Create service
    service = PaddleOcrService(
        model=model,
        language=language,
        use_gpu=use_gpu,
        gpu_mem=gpu_mem
    )
    
    print("  Initializing PaddleOCR (loading models)...", flush=True)
    if not service.initialize():
        raise RuntimeError("Failed to initialize PaddleOCR service")
    
    print("  PaddleOCR initialized successfully!", flush=True)
    
    # Create servicer
    servicer = PaddleOcrServicer(service)
    
    print("  Creating gRPC server...", flush=True)
    # Create server
    server = grpc.server(
        futures.ThreadPoolExecutor(max_workers=max_workers),
        options=[
            ("grpc.max_receive_message_length", 100 * 1024 * 1024),  # 100MB
            ("grpc.max_send_message_length", 100 * 1024 * 1024),
        ]
    )
    
    # Add generic handler for our service
    server.add_generic_rpc_handlers((GenericPaddleOcrHandler(servicer),))
    
    print(f"  Binding to port {port}...", flush=True)
    server.add_insecure_port(f"[::]:{port}")
    
    print("  Server created!", flush=True)
    return server


class GenericPaddleOcrHandler(grpc.GenericRpcHandler):
    """Generic RPC handler for PaddleOCR service."""
    
    def __init__(self, servicer: PaddleOcrServicer):
        self.servicer = servicer
    
    def service(self, handler_call_details):
        """Route RPC calls to the appropriate handler."""
        method = handler_call_details.method
        
        if method == "/cascade.vision.PaddleOcrService/Recognize":
            return grpc.unary_unary_rpc_method_handler(
                self.servicer.Recognize,
                request_deserializer=lambda x: x,
                response_serializer=lambda x: x
            )
        elif method == "/cascade.vision.PaddleOcrService/RecognizeBatch":
            return grpc.stream_stream_rpc_method_handler(
                self.servicer.RecognizeBatch,
                request_deserializer=lambda x: x,
                response_serializer=lambda x: x
            )
        elif method == "/cascade.vision.PaddleOcrService/GetStatus":
            return grpc.unary_unary_rpc_method_handler(
                self.servicer.GetStatus,
                request_deserializer=lambda x: x,
                response_serializer=lambda x: x
            )
        
        return None


def main():
    """Main entry point for the server."""
    import sys
    
    parser = argparse.ArgumentParser(
        description="PaddleOCR gRPC Server"
    )
    parser.add_argument(
        "--port", 
        type=int, 
        default=50052,
        help="Port to listen on (default: 50052)"
    )
    parser.add_argument(
        "--model",
        choices=["vitstr", "svtr", "ppocrv4"],
        default="ppocrv4",
        help="OCR model to use (default: ppocrv4)"
    )
    parser.add_argument(
        "--language",
        default="en",
        help="Language for recognition (default: en)"
    )
    parser.add_argument(
        "--gpu",
        action="store_true",
        help="Use GPU acceleration"
    )
    parser.add_argument(
        "--gpu-mem",
        type=int,
        default=500,
        help="GPU memory limit in MB (default: 500)"
    )
    parser.add_argument(
        "--workers",
        type=int,
        default=4,
        help="Number of worker threads (default: 4)"
    )
    parser.add_argument(
        "--log-level",
        choices=["DEBUG", "INFO", "WARNING", "ERROR"],
        default="INFO",
        help="Logging level (default: INFO)"
    )
    
    args = parser.parse_args()
    
    # Configure logging with flush
    logging.basicConfig(
        level=getattr(logging, args.log_level),
        format="%(asctime)s - %(name)s - %(levelname)s - %(message)s",
        stream=sys.stdout
    )
    
    print(f"Starting PaddleOCR gRPC Server on port {args.port}", flush=True)
    print(f"Model: {args.model}, Language: {args.language}, GPU: {args.gpu}", flush=True)
    
    try:
        print("Creating server...", flush=True)
        server = create_server(
            port=args.port,
            model=args.model,
            language=args.language,
            use_gpu=args.gpu,
            gpu_mem=args.gpu_mem,
            max_workers=args.workers
        )
        
        print("Starting gRPC server...", flush=True)
        server.start()
        print(f"=" * 50, flush=True)
        print(f"SERVER READY - Listening on port {args.port}", flush=True)
        print(f"=" * 50, flush=True)
        
        # Handle shutdown signals
        def handle_shutdown(signum, frame):
            print("Shutting down server...", flush=True)
            server.stop(grace=5)
            sys.exit(0)
        
        signal.signal(signal.SIGINT, handle_shutdown)
        signal.signal(signal.SIGTERM, handle_shutdown)
        
        # Wait for termination
        server.wait_for_termination()
        
    except Exception as e:
        print(f"ERROR: Failed to start server: {e}", flush=True)
        import traceback
        traceback.print_exc()
        sys.exit(1)


if __name__ == "__main__":
    main()

