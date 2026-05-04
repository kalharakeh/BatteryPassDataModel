type LoginUser = {
  roles: string[];
};

function safeRelativePath(value: string) {
  if (!value.startsWith("/") || value.startsWith("//")) return "";
  try {
    const parsed = new URL(value, "http://localhost");
    return `${parsed.pathname}${parsed.search}${parsed.hash}`;
  } catch {
    return "";
  }
}

export function loginRedirectDestination(user: LoginUser, nextPath: string) {
  const safeNext = safeRelativePath(nextPath);
  if (safeNext) return safeNext;
  return user.roles.includes("admin") ? "/admin" : "/";
}
