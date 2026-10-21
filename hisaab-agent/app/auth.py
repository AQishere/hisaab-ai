"""Verify the ERP's JWT locally, so logs and limits use a trusted user id without a round trip to the ERP.

The ERP still verifies the same token on every tool call, which also enforces deactivated accounts and changed
roles or passwords.
"""

from dataclasses import dataclass

import jwt

NAME_ID_CLAIM = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier"
NAME_CLAIM = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name"
ROLE_CLAIM = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role"


class AuthError(Exception):
    pass


@dataclass(frozen=True)
class CurrentUser:
    id: int
    username: str
    role: str
    token: str


def bearer_token(authorization: str | None) -> str:
    if not authorization or not authorization.lower().startswith("bearer "):
        raise AuthError("Sign in to the ERP first.")
    return authorization[7:].strip()


def verify(token: str, key: str, issuer: str, audience: str) -> CurrentUser:
    if not key:
        raise AuthError("The assistant is not configured: AGENT_JWT_KEY is missing.")
    try:
        # One minute of leeway, the same clock skew the ERP allows.
        claims = jwt.decode(token, key, algorithms=["HS256"], issuer=issuer, audience=audience, leeway=60,
                            options={"require": ["exp", "iss", "aud"]})
        return CurrentUser(int(claims[NAME_ID_CLAIM]), str(claims[NAME_CLAIM]), str(claims[ROLE_CLAIM]), token)
    except (jwt.PyJWTError, KeyError, ValueError) as exc:
        raise AuthError("Sign in again.") from exc
