import argparse
import re
from google.oauth2 import service_account
from googleapiclient.discovery import build
from googleapiclient.http import MediaFileUpload

def get_release_name(service, package_name, track_name, build_number):
    """
    Looks up previous releases on Play Console.
    If 'Release <build_number> TEST <ZZ>' exists, increments ZZ.
    Otherwise, returns 'Release <build_number> TEST 1'.
    """
    try:
        track_info = service.edits().tracks().get(
            packageName=package_name,
            editId='latest',
            track=track_name
        ).execute()

        releases = track_info.get('releases', [])
        pattern = re.compile(rf'Release\s+{build_number}\s+TEST\s+(\d+)')
        
        max_test_num = 0
        for r in releases:
            name = r.get('name', '')
            match = pattern.search(name)
            if match:
                test_num = int(match.group(1))
                if test_num > max_test_num:
                    max_test_num = test_num

        next_test_num = max_test_num + 1
        return f"Release {build_number} TEST {next_test_num}"

    except Exception:
        return f"Release {build_number} TEST 1"


def upload_aab(bundle_path, package_name, json_key_path, track_name, build_number):
    credentials = service_account.Credentials.from_service_account_file(
        json_key_path,
        scopes=['https://www.googleapis.com/auth/androidpublisher']
    )
    
    service = build('androidpublisher', 'v3', credentials=credentials)

    # 1. Create Edit
    edit_request = service.edits().insert(body={}, packageName=package_name)
    edit_id = edit_request.execute()['id']

    # 2. Upload AAB Bundle
    media = MediaFileUpload(bundle_path, mimetype='application/octet-stream', resumable=True)
    bundle_response = service.edits().bundles().upload(
        packageName=package_name,
        editId=edit_id,
        media_body=media
    ).execute()

    version_code = bundle_response['versionCode']

    # 3. Generate "Release XX TEST ZZ" string
    release_name = get_release_name(service, package_name, track_name, build_number)

    # 4. Assign release details to target track
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

    # 5. Commit Edit
    service.edits().commit(packageName=package_name, editId=edit_id).execute()
    print(f"[AutoBuilder] Uploaded VersionCode {version_code} as '{release_name}' to '{track_name}' track!")

if __name__ == '__main__':
    parser = argparse.ArgumentParser(description="Upload AAB to Google Play Console")
    parser.add_argument('--bundle', required=True, help="Path to AAB file")
    parser.add_argument('--package', required=True, help="Package Name")
    parser.add_argument('--json', required=True, help="Service Account JSON path")
    parser.add_argument('--track', default='internal', help="Target track")
    parser.add_argument('--build-number', required=True, help="File Build Number (e.g. 63)")

    args = parser.parse_args()
    upload_aab(args.bundle, args.package, args.json, args.track, args.build_number)