"""
Generated gRPC stubs from cascade.proto

This directory contains auto-generated files:
- cascade_pb2.py (message classes)
- cascade_pb2_grpc.py (service stubs)

To regenerate, run:
  python generate_proto.ps1  (Windows)
  ./generate_proto.sh        (Linux/Mac)
"""

# Import generated modules when available
try:
    from cascade_client.proto import cascade_pb2
    from cascade_client.proto import cascade_pb2_grpc

    __all__ = ["cascade_pb2", "cascade_pb2_grpc"]
except ImportError:
    # Proto files not generated yet
    __all__ = []

