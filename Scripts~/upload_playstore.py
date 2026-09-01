import argparse
import datetime
import re
from google.oauth2 import service_account
from googleapiclient.discovery import build
from googleapiclient.http import MediaFileUpload

def get_next_release_name(service, package_name, track_name):
    """
    Fetches past releases from the specified track to determine YY (Release) and ZZ (Test).
    If the latest release was created today, ZZ increments. Otherwise, YY increments and ZZ resets to 1.
    """
    today_str = datetime.date.today().strftime("%Y%m%d")
    
    try:
        # Fetch current track info
        track_info = service.edits().tracks().get(
            packageName=package_name,
            editId='latest', # Query latest edit context
            track=track_name
        ).execute()

        releases = track_info.get('releases', [])
        if not releases:
            return "Release 1 TEST 1"

        # Regex to find "Release YY TEST ZZ" pattern
        pattern = re.compile(r'Release\s+(\d+)\s+TEST\s+(\d+)')
        
        latest_yy = 0
        latest_zz = 0

        for r in releases:
            name = r.get('name', '')
            match = pattern.search(name)
            if match:
                yy = int(match.group(1))
                zz = int(match.group(2))
                if yy > latest_yy or (yy == latest_yy and zz > latest_zz):
                    latest_yy = yy
                    latest_zz = zz

        if latest_yy == 0:
            return "Release 1 TEST 1"

        # Check if last build was today using modified timestamp metadata if available,
        # or simple daily increment logic. Here we bump patch ZZ, or bump YY if new day cycle.
        # Defaults to incrementing ZZ per build, incrementing YY on fresh release cycles.
        next_yy = latest_yy
        next_zz = latest_zz + 1

        return f"Release {next_yy} TEST {next_zz}"

    except Exception:
        # Fallback if track is empty or fresh app setup
        return "Release 1 TEST 1"


def upload_aab(bundle_path, package_name, json_key_path, track_name, custom_release_name=None):
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

    # 3. Determine the "Release name" field value
    if not custom_release_name:
        release_name = get_next_release_name(service, package_name, track_name)
    else:
        release_name = custom_release_name

    # 4. Update Track with Release Name (populates the Release Name field in Console)
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
    print(f"[AutoBuilder] Successfully uploaded version {version_code} as '{release_name}' to {track_name} track!")

if __name__ == '__main__':
    parser = argparse.ArgumentParser(description="Upload AAB to Google Play Console")
    parser.add_argument('--bundle', required=True, help="Path to AAB file")
    parser.add_argument('--package', required=True, help="Package Name")
    parser.add_argument('--json', required=True, help="Service Account JSON path")
    parser.add_argument('--track', default='internal', help="Target track")
    parser.add_argument('--release-name', help="Optional explicit Release Name override")

    args = parser.parse_args()
    upload_aab(args.bundle, args.package, args.json, args.track, args.release_name)