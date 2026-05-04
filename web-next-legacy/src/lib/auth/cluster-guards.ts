import { notFound, redirect } from "next/navigation";
import {
  administeredClusterIdsForSession,
  canAdministerCluster,
  canOpenPassportDetail,
  filterMembershipsForClusterAdmin,
  filterPassportsForRegistryAccess,
  hasAnyClusterAdminMembership,
  isGeneralAdmin,
} from "./cluster-access";
import { getSession } from "./session";
import { getClusterMembershipsForUser, listClusterMemberships } from "@/lib/db/clusters";
import { getPassportForAdmin, listPassports } from "@/lib/db/passports";
import type { BatteryPassport } from "@/types/passport";

async function requireSession() {
  const session = await getSession();
  if (!session) {
    redirect("/login");
  }
  return session;
}

function loginPath(nextPath?: string) {
  return nextPath ? `/login?next=${encodeURIComponent(nextPath)}` : "/login";
}

async function requireSessionFor(nextPath?: string) {
  const session = await getSession();
  if (!session) {
    redirect(loginPath(nextPath));
  }
  return session;
}

export async function requirePassportDetailAccess(
  passport: BatteryPassport,
  options: { nextPath?: string; unauthorizedRedirectPath?: string } = {},
) {
  const session = await requireSessionFor(options.nextPath);
  const memberships = await getClusterMembershipsForUser(session.email);
  if (!canOpenPassportDetail(session, passport, memberships)) {
    if (options.unauthorizedRedirectPath) {
      redirect(options.unauthorizedRedirectPath);
    }
    notFound();
  }
  return session;
}

export async function requireClusterAdminAccess(passport: BatteryPassport) {
  const session = await requireSession();
  const memberships = await getClusterMembershipsForUser(session.email);
  if (!canAdministerCluster(session, passport.clusterId, memberships)) {
    notFound();
  }
  return session;
}

export async function requireClusterAdminForCluster(clusterId: string) {
  const session = await requireSession();
  const memberships = await getClusterMembershipsForUser(session.email);
  if (!canAdministerCluster(session, clusterId, memberships)) {
    notFound();
  }
  return { session, memberships };
}

export async function requireClusterAdminSession() {
  const session = await requireSession();
  const memberships = await getClusterMembershipsForUser(session.email);
  const administeredClusterIds = administeredClusterIdsForSession(session, memberships);
  if (isGeneralAdmin(session) || administeredClusterIds.length === 0) {
    notFound();
  }
  return { session, memberships, administeredClusterIds };
}

export async function listClusterAdminUserMemberships() {
  const scope = await requireClusterAdminSession();
  const memberships = await listClusterMemberships();
  return {
    ...scope,
    memberships: filterMembershipsForClusterAdmin(scope.session, memberships),
  };
}

export async function listPassportsForRegistryAccess(query = "") {
  const session = await requireSession();
  const memberships = await getClusterMembershipsForUser(session.email);
  if (!isGeneralAdmin(session) && !hasAnyClusterAdminMembership(session, memberships)) {
    notFound();
  }

  const passports = await listPassports(query, { includeArchived: isGeneralAdmin(session) });
  return filterPassportsForRegistryAccess(session, passports, memberships);
}

export async function listClusterAdminPassports(query = "") {
  const session = await requireSession();
  const memberships = await getClusterMembershipsForUser(session.email);
  if (!hasAnyClusterAdminMembership(session, memberships) || isGeneralAdmin(session)) {
    return [];
  }

  const passports = await listPassports(query);
  return filterPassportsForRegistryAccess(session, passports, memberships);
}

function nestedMetadata(metadata: Record<string, unknown>) {
  return ((metadata.metadata ?? {}) as Record<string, unknown>) || {};
}

export async function requireFileDownloadAccess(fileMetadata: Record<string, unknown>) {
  const metadata = nestedMetadata(fileMetadata);
  if (metadata.publicAccess === "summaryImage" || metadata.documentKey === "batteryImage") {
    return;
  }

  const passportId = typeof metadata.passportId === "string" ? metadata.passportId : "";
  if (!passportId) {
    const session = await requireSession();
    if (!isGeneralAdmin(session)) {
      notFound();
    }
    return;
  }

  const passport = await getPassportForAdmin(passportId);
  if (!passport) {
    notFound();
  }
  await requirePassportDetailAccess(passport);
}
