#!/bin/bash
# Bash script to set up Python virtual environment for Cascade
# Usage: ./setup_venv.sh

set -e

echo "Setting up Cascade Python virtual environment..."

# Check Python version
if ! command -v python3 &> /dev/null; then
    echo "Error: Python 3 is not installed or not in PATH"
    exit 1
fi

PYTHON_VERSION=$(python3 --version)
echo "Found: $PYTHON_VERSION"

# Create virtual environment
VENV_PATH="$(dirname "$0")/.venv"
if [ -d "$VENV_PATH" ]; then
    echo "Virtual environment already exists at $VENV_PATH"
    echo "Removing existing virtual environment..."
    rm -rf "$VENV_PATH"
fi

echo "Creating virtual environment at $VENV_PATH..."
python3 -m venv "$VENV_PATH"

# Activate virtual environment
echo "Activating virtual environment..."
source "$VENV_PATH/bin/activate"

# Upgrade pip
echo "Upgrading pip..."
pip install --upgrade pip

# Install dependencies
echo "Installing dependencies from requirements.txt..."
pip install -r requirements.txt

# Generate proto stubs
echo "Generating gRPC stubs from proto files..."
"$(dirname "$0")/generate_proto.sh"

echo ""
echo "Setup complete! Virtual environment is ready."
echo "To activate the virtual environment, run:"
echo "  source .venv/bin/activate"

