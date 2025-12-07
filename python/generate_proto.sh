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

echo "Successfully generated gRPC stubs!"
echo "Generated files:"
ls -1 "$OUTPUT_DIR" | sed 's/^/  - /'

