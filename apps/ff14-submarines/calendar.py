from __future__ import annotations

import os
from datetime import datetime, timedelta
from typing import Optional, Tuple

from google.oauth2.credentials import Credentials
from google_auth_oauthlib.flow import InstalledAppFlow
from google.auth.transport.requests import Request
from googleapiclient.discovery import build

from .config import DATA_DIR, TOKEN_PATH

# If modifying scopes, delete the token.
SCOPES = ["https://www.googleapis.com/auth/calendar"]


def get_service() -> "Resource":
    creds = None
    if os.path.exists(TOKEN_PATH):
        creds = Credentials.from_authorized_user_file(TOKEN_PATH, SCOPES)
    if not creds or not creds.valid:
        if creds and creds.expired and creds.refresh_token:
            creds.refresh(Request())
        else:
            flow = InstalledAppFlow.from_client_secrets_file(
                os.path.join(os.getcwd(), "credentials.json"), SCOPES
            )
            creds = flow.run_local_server(port=0)
        with open(TOKEN_PATH, "w", encoding="utf-8") as token:
            token.write(creds.to_json())
    service = build("calendar", "v3", credentials=creds)
    return service


def iso(dt: datetime) -> str:
    # Google API expects RFC3339 with timezone offset
    return dt.astimezone().isoformat()


def ensure_event(
    service,
    calendar_id: str,
    title: str,
    start: datetime,
    end: datetime,
    private_key: str,
    reminder_minutes: int,
) -> str:
    # Prefer search by extended private property to uniquely identify
    events_result = (
        service.events()
        .list(
            calendarId=calendar_id,
            privateExtendedProperty=f"ff14_sub_key={private_key}",
            maxResults=1,
            singleEvents=True,
            orderBy="startTime",
        )
        .execute()
    )
    events = events_result.get("items", [])

    body = {
        "summary": title,
        "start": {"dateTime": iso(start)},
        "end": {"dateTime": iso(end)},
        "reminders": {
            "useDefault": False,
            "overrides": [{"method": "popup", "minutes": reminder_minutes}],
        },
        "extendedProperties": {"private": {"ff14_sub_key": private_key}},
    }

    if events:
        event_id = events[0]["id"]
        service.events().update(calendarId=calendar_id, eventId=event_id, body=body).execute()
        return event_id
    else:
        created = service.events().insert(calendarId=calendar_id, body=body).execute()
        return created["id"]

