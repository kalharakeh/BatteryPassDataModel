import type { BatteryPassport, ClusterMembership } from "@/types/passport";

export type SessionLike = {
  email: string;
  roles: string[];
  iat?: number;
} | null;

function normalizedEmail(email: string) {
  return email.trim().toLowerCase();
}

function membershipsForSession(session: SessionLike, memberships: ClusterMembership[]) {
  if (!session) return [];
  const email = normalizedEmail(session.email);
  return memberships.filter((membership) => normalizedEmail(membership.email) === email);
}

export function isGeneralAdmin(session: SessionLike) {
  return Boolean(session?.roles.includes("admin"));
}

export function hasClusterMembership(session: SessionLike, clusterId: string | undefined, memberships: ClusterMembership[]) {
  if (!session || !clusterId) return false;
  return membershipsForSession(session, memberships).some((membership) => membership.clusterId === clusterId);
}

export function canOpenPassportDetail(session: SessionLike, passport: BatteryPassport, memberships: ClusterMembership[]) {
  if (isGeneralAdmin(session)) return true;
  return hasClusterMembership(session, passport.clusterId, memberships);
}

export function canAdministerCluster(session: SessionLike, clusterId: string | undefined, memberships: ClusterMembership[]) {
  if (isGeneralAdmin(session)) return true;
  if (!session || !clusterId) return false;
  return membershipsForSession(session, memberships).some(
    (membership) => membership.clusterId === clusterId && membership.role === "clusterAdmin",
  );
}

export function hasAnyClusterAdminMembership(session: SessionLike, memberships: ClusterMembership[]) {
  if (isGeneralAdmin(session)) return true;
  return membershipsForSession(session, memberships).some((membership) => membership.role === "clusterAdmin");
}

export function administeredClusterIdsForSession(session: SessionLike, memberships: ClusterMembership[]) {
  if (!session || isGeneralAdmin(session)) return [];
  return membershipsForSession(session, memberships)
    .filter((membership) => membership.role === "clusterAdmin")
    .map((membership) => membership.clusterId)
    .sort();
}

export function filterMembershipsForClusterAdmin(session: SessionLike, memberships: ClusterMembership[]) {
  const administeredClusters = new Set(administeredClusterIdsForSession(session, memberships));
  return memberships.filter((membership) => administeredClusters.has(membership.clusterId));
}

export function filterPassportsForRegistryAccess(
  session: SessionLike,
  passports: BatteryPassport[],
  memberships: ClusterMembership[],
) {
  if (isGeneralAdmin(session)) return passports;
  const administeredClusters = new Set(administeredClusterIdsForSession(session, memberships));

  return passports.filter((passport) => passport.clusterId && administeredClusters.has(passport.clusterId));
}
