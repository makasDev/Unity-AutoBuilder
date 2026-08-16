import sys
import argparse
import os
import io
from google.oauth2 import service_account
from googleapiclient.discovery import build
from googleapiclient.http import MediaFileUpload

# Force UTF-8 output encoding for Windows terminals
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')

def upload_aab(bundle_path, package_name, json_key_path, track):
    if not os.path.exists(json_key_path):
        print(f"[Error] Service account file '{json_key_path}' not found!")
        sys.exit(1)

    print(f"Connecting to Google Play API for {package_name}...")
    
    credentials = service_account.Credentials.from_service_account_file(
        json_key_path,
        scopes=['https://www.googleapis.com/auth/androidpublisher']
    )
    
    service = build('androidpublisher', 'v3', credentials=credentials)
    
    # 1. Create edit transaction
    edit_request = service.edits().insert(packageName=package_name, body={})
    edit_response = edit_request.execute()
    edit_id = edit_response['id']
    print(f"Created edit session: {edit_id}")

    # 2. Upload AAB file
    print(f"Uploading AAB: {bundle_path}...")
    media = MediaFileUpload(bundle_path, mimetype='application/octet-stream', resumable=True)
    upload_request = service.edits().bundles().upload(
        packageName=package_name,
        editId=edit_id,
        media_body=media
    )
    
    bundle_response = upload_request.execute()
    uploaded_code = bundle_response['versionCode']
    print(f"AAB uploaded successfully with Version Code: {uploaded_code}")

    # 3. Assign build to selected track
    print(f"Assigning build to track: '{track}'...")
    track_request = service.edits().tracks().update(
        packageName=package_name,
        editId=edit_id,
        track=track,
        body={
            "releases": [{
                "versionCodes": [str(uploaded_code)],
                "status": "completed"
            }]
        }
    )
    track_request.execute()

    # 4. Commit changes
    print("Committing release to Google Play...")
    commit_request = service.edits().commit(
        packageName=package_name,
        editId=edit_id
    )
    commit_request.execute()
    print("SUCCESS! Build pushed to track: " + track)

if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument('--bundle', required=True, help="Path to .aab file")
    parser.add_argument('--code', required=False, help="Version code")
    parser.add_argument('--package', required=True, help="Package name")
    parser.add_argument('--json', required=True, help="Path to service account JSON")
    parser.add_argument('--track', default="internal", help="Target track (internal, alpha, beta, production)")
    
    args = parser.parse_args()

    upload_aab(args.bundle, args.package, args.json, args.track)