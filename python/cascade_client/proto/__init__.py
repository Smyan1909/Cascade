"""
Generated gRPC stubs from cascade.proto

This directory contains auto-generated files:
- cascade_pb2.py (message classes)
- cascade_pb2_grpc.py (service stubs)

To regenerate, run:
  python generate_proto.ps1  (Windows)
  ./generate_proto.sh        (Linux/Mac)
"""

from __future__ import annotations

# NOTE:
# The generated `cascade_pb2_grpc.py` uses a top-level import:
#   `import cascade_pb2 as cascade__pb2`
# which can fail when these stubs live inside a package (this directory).
# We avoid editing generated code by ensuring this directory is on `sys.path`
# so that `import cascade_pb2` succeeds.
import sys
from pathlib import Path

_proto_dir = Path(__file__).resolve().parent
_proto_dir_str = str(_proto_dir)
if _proto_dir_str not in sys.path:
    sys.path.insert(0, _proto_dir_str)

# Import generated modules when available
try:
    from cascade_client.proto import cascade_pb2
    from cascade_client.proto import cascade_pb2_grpc

    __all__ = ["cascade_pb2", "cascade_pb2_grpc"]
except ImportError:
    # Proto files not generated yet
    __all__ = []

