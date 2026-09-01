import argparse
from google.oauth2 import service_account
from googleapiclient.discovery import build
from googleapiclient.http import MediaFileUpload

def upload_aab(bundle_path, package_name, json_key_path, track_name, release_name):
    credentials = service_account.Credentials.from_service_account_file(
        json_key_path,
        scopes=['https://www.googleapis.com/auth/androidpublisher']
    )
    
    service = build('androidpublisher', 'v3', credentials=credentials)

    # 1. Create Edit
    edit_request = service.edits().insert(body={}, packageName=package_name)
    edit_id = edit_request.execute()['id']

    # 2. Upload Bundle
    media = MediaFileUpload(bundle_path, mimetype='application/octet-stream', resumable=True)
    bundle_response = service.edits().bundles().upload(
        packageName=package_name,
        editId=edit_id,
        media_body=media
    ).execute()

    version_code = bundle_response['versionCode']

    # 3. Assign to Track with Release Name
    release_body = {
        'name': release_name,
        'versionCodes': [str(version_code)],
        'status': 'completed'
    }

    track_body = {
        'track': track_name,
        'releases': [release_body]
    }

    service.edits().tracks().update(
        packageName=package_name,
        editId=edit_id,
        track=track_name,
        body=track_body
    ).execute()

    # 4. Commit Edit
    service.edits().commit(packageName=package_name, editId=edit_id).execute()
    print(f"[AutoBuilder] Successfully uploaded VersionCode {version_code} as '{release_name}' to '{track_name}' track!")

if __name__ == '__main__':
    parser = argparse.ArgumentParser(description="Upload AAB to Google Play Console")
    parser.add_argument('--bundle', required=True, help="Path to AAB file")
    parser.add_argument('--package', required=True, help="Package Name")
    parser.add_argument('--json', required=True, help="Service Account JSON path")
    parser.add_argument('--track', default='internal', help="Target track")
    parser.add_argument('--release-name', required=True, help="Release Title string (e.g. Release 63 TEST 1)")

    args = parser.parse_args()
    upload_aab(args.bundle, args.package, args.json, args.track, args.release_name)