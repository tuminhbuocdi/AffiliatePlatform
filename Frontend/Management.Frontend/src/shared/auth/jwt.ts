export type JwtPayload = {
  exp?: number
  role?: string
  roles?: string[]
  ["http://schemas.microsoft.com/ws/2008/06/identity/claims/role"]?: string
  [k: string]: unknown
}

export function readJwtPayload(token: string): JwtPayload | null {
  try {
    const parts = token.split(".")
    const base64Url = parts[1]
    if (parts.length < 2 || !base64Url) return null

    const base64 = base64Url.replace(/-/g, "+").replace(/_/g, "/")
    const jsonPayload = decodeURIComponent(
      atob(base64)
        .split("")
        .map((c) => "%" + ("00" + c.charCodeAt(0).toString(16)).slice(-2))
        .join(""),
    )

    return JSON.parse(jsonPayload) as JwtPayload
  } catch {
    return null
  }
}

export function isTokenExpired(token: string, nowMs = Date.now()): boolean {
  const payload = readJwtPayload(token)
  const exp = payload?.exp
  if (!exp) return true

  const nowSec = Math.floor(nowMs / 1000)
  return nowSec >= exp
}

export function clearAuthSession() {
  localStorage.removeItem("token")
}

export function getJwtRole(token: string): string | null {
  const payload = readJwtPayload(token)
  if (!payload) return null

  const claimRole = payload["http://schemas.microsoft.com/ws/2008/06/identity/claims/role"]
  if (typeof claimRole === "string" && claimRole.trim()) return claimRole

  if (typeof payload.role === "string" && payload.role.trim()) return payload.role

  const roles = payload.roles
  if (Array.isArray(roles) && roles.length > 0) {
    const first = roles.find((x) => typeof x === "string" && x.trim())
    return first ?? null
  }

  return null
}

export function isAdminToken(token: string): boolean {
  const role = getJwtRole(token)
  return role === "admin"
}
