import type { Cluster, ClusterMembership, ClusterMembershipRole } from "@/types/passport";
import { clusterCollection, clusterMembershipCollection } from "./collections";

function normalizedEmail(email: string) {
  return email.trim().toLowerCase();
}

export async function listClusters() {
  const collection = await clusterCollection();
  return collection.find({}, { projection: { _id: 0 } }).sort({ name: 1 }).toArray() as Promise<Cluster[]>;
}

export async function getClusterMembershipsForUser(email: string) {
  const collection = await clusterMembershipCollection();
  return collection
    .find({ email: normalizedEmail(email) }, { projection: { _id: 0 } })
    .sort({ clusterId: 1 })
    .toArray() as Promise<ClusterMembership[]>;
}

export async function listClusterMemberships() {
  const collection = await clusterMembershipCollection();
  return collection.find({}, { projection: { _id: 0 } }).sort({ email: 1, clusterId: 1 }).toArray() as Promise<ClusterMembership[]>;
}

export async function upsertCluster(cluster: Cluster) {
  const collection = await clusterCollection();
  await collection.updateOne({ clusterId: cluster.clusterId }, { $set: cluster, $unset: { kind: "" } }, { upsert: true });
  return cluster;
}

export async function updateClusterName(clusterId: string, name: string, now = new Date().toISOString()) {
  const collection = await clusterCollection();
  await collection.updateOne({ clusterId }, { $set: { name, updatedAt: now }, $unset: { kind: "" } });
}

export async function upsertClusterMembership(input: {
  email: string;
  clusterId: string;
  role: ClusterMembershipRole;
  now?: string;
}) {
  const collection = await clusterMembershipCollection();
  const now = input.now ?? new Date().toISOString();
  const email = normalizedEmail(input.email);
  await collection.updateOne(
    { email, clusterId: input.clusterId },
    {
      $set: {
        email,
        clusterId: input.clusterId,
        role: input.role,
        updatedAt: now,
      },
      $setOnInsert: {
        createdAt: now,
      },
    },
    { upsert: true },
  );
}

export async function deleteClusterMembership(email: string, clusterId: string) {
  const collection = await clusterMembershipCollection();
  await collection.deleteOne({ email: normalizedEmail(email), clusterId });
}

export async function deleteCluster(clusterId: string) {
  const clusters = await clusterCollection();
  const memberships = await clusterMembershipCollection();
  await clusters.deleteOne({ clusterId });
  await memberships.deleteMany({ clusterId });
}
