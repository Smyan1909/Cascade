#!/bin/bash
# Bash script to generate Python gRPC stubs from proto files
# Usage: ./generate_proto.sh

set -e

echo "Generating Python gRPC stubs..."

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROTO_DIR="$(dirname "$SCRIPT_DIR")/proto"
OUTPUT_DIR="$SCRIPT_DIR/cascade_client/proto"

# Create output directory if it doesn't exist
mkdir -p "$OUTPUT_DIR"
echo "Output directory: $OUTPUT_DIR"

PROTO_FILE="$PROTO_DIR/cascade.proto"
if [ ! -f "$PROTO_FILE" ]; then
    echo "Error: Proto file not found at $PROTO_FILE"
    exit 1
fi

echo "Proto file: $PROTO_FILE"

# Generate Python stubs
python3 -m grpc_tools.protoc \
    -I "$PROTO_DIR" \
    --python_out="$OUTPUT_DIR" \
    --grpc_python_out="$OUTPUT_DIR" \
    "$PROTO_FILE"

# Patch generated gRPC stubs to use package-safe relative imports.
# grpc_tools can emit e.g. `import cascade_pb2 as cascade__pb2`, which fails when
# the stubs are used as a package (`cascade_client.proto`).
for f in "$OUTPUT_DIR"/*_pb2_grpc.py; do
    [ -f "$f" ] || continue
    # Replace:
    #   import foo_pb2 as foo__pb2
    # with:
    #   from . import foo_pb2 as foo__pb2
    perl -0777 -pe 's/^import\s+(\w+_pb2)\s+as\s+(\w+)\s*$/from . import $1 as $2/gm; s/^import\s+(\w+_pb2)\s*$/from . import $1/gm' -i "$f"
    echo "Patched imports in: $(basename "$f")"
done

echo "Successfully generated gRPC stubs!"
echo "Generated files:"
ls -1 "$OUTPUT_DIR" | sed 's/^/  - /'

