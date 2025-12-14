"""
Quick test script to verify Firestore emulator connection and save a test skill map.
Run this with the emulator running on localhost:8080

Usage:
    cd python
    .\.venv\Scripts\activate
    $env:FIRESTORE_EMULATOR_HOST="localhost:8080"
    python test_firestore_quick.py
"""

import os
import sys

# Check emulator is configured
if not os.getenv("FIRESTORE_EMULATOR_HOST"):
    print("ERROR: Set FIRESTORE_EMULATOR_HOST=localhost:8080 first")
    sys.exit(1)

print(f"Using emulator at: {os.getenv('FIRESTORE_EMULATOR_HOST')}")

try:
    from google.cloud import firestore
    from google.auth import credentials as cred
    
    # Connect to emulator
    client = firestore.Client(
        project='cascade-prototype',
        credentials=cred.AnonymousCredentials()
    )
    print(f"Connected to project: {client.project}")
    
    # Save a test document
    test_doc = {
        "metadata": {
            "skill_id": "test-quick-verify",
            "app_id": "cascade-prototype", 
            "user_id": "test-user",
            "capability": "Quick Verification Test",
            "description": "This is a test skill map to verify emulator connection",
            "version": 1,
        },
        "steps": [
            {"action": "Click", "step_description": "Test step"}
        ]
    }
    
    # Save to Firestore
    doc_path = "artifacts/cascade-prototype/users/test-user/skill_maps/test-quick-verify"
    print(f"Saving to: {doc_path}")
    client.document(doc_path).set(test_doc)
    print("SUCCESS: Document saved!")
    
    # Read it back
    doc = client.document(doc_path).get()
    if doc.exists:
        data = doc.to_dict()
        print(f"SUCCESS: Read back - capability: {data['metadata']['capability']}")
    
    # List all skill maps
    print("\n=== All Skill Maps ===")
    collection = client.collection("artifacts/cascade-prototype/users/test-user/skill_maps")
    docs = list(collection.stream())
    print(f"Found {len(docs)} skill maps:")
    for d in docs:
        data = d.to_dict()
        cap = data.get("metadata", {}).get("capability", "Unknown")
        print(f"  - {d.id}: {cap}")
    
    print("\n✅ Firestore emulator is working correctly!")
    print("   Open http://127.0.0.1:4000/firestore to see the data")
    print("   Navigate to: artifacts > cascade-prototype > users > test-user > skill_maps")

except Exception as e:
    print(f"ERROR: {e}")
    import traceback
    traceback.print_exc()
