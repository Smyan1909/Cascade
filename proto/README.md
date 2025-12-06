# proto/

`cascade.proto` defines the contract between the Python Brain and C# Body. Follow `docs/phase1-grpc.md` and `docs/00-overview.md` for architecture and data boundaries.

## Codegen (run later)
- Python: `python -m grpc_tools.protoc -I proto --python_out=. --grpc_python_out=. proto/cascade.proto`
- C#: install `dotnet-grpc` or use `Grpc.Tools`, e.g.  
  `dotnet-grpc --proto proto/cascade.proto --csharp_out src/Body/Generated --grpc_out src/Body/Generated --services=Server --csharp_opt=base_namespace=Cascade.Proto`
- Treat the proto as the single source of truth; do not edit generated code.

## Contract testing
- Lint: `buf lint` (or `protoc --lint_out` equivalent) in CI.
- Golden snapshot: generate stubs in CI and compare against committed snapshots to detect breaking changes.
- Backward compatibility: additive-only changes; never reuse/renumber fields; bump major for breaking changes.
- Interop smoke: minimal mock server/client does one round-trip per RPC with dummy payloads to verify channel wiring.

