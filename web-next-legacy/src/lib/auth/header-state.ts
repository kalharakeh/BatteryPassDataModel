import { hasAnyClusterAdminMembership, isGeneralAdmin, type SessionLike } from "./cluster-access";
import type { Cluster, ClusterMembership } from "@/types/passport";

type HeaderLink = {
  href: string;
  label: string;
};

export function buildHeaderState(session: SessionLike, memberships: ClusterMembership[], clusters: Cluster[]) {
  if (!session) {
    return {
      identityLabel: "",
      clusterNames: [] as string[],
      links: [] as HeaderLink[],
    };
  }

  const clusterNameById = new Map(clusters.map((cluster) => [cluster.clusterId, cluster.name]));
  const clusterNames = memberships
    .filter((membership) => membership.email.toLowerCase() === session.email.toLowerCase())
    .map((membership) => clusterNameById.get(membership.clusterId) ?? membership.clusterId);

  let links: HeaderLink[] = [];
  if (isGeneralAdmin(session)) {
    links = [{ href: "/admin", label: "Admin" }];
  } else if (hasAnyClusterAdminMembership(session, memberships)) {
    links = [{ href: "/cluster-admin", label: "Cluster admin" }];
  }

  return {
    identityLabel: session.email,
    clusterNames,
    links,
  };
}
