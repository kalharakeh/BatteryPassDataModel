"use server";

import { randomUUID } from "node:crypto";
import { revalidatePath } from "next/cache";
import { requireRole } from "@/lib/auth/session";
import { deleteCluster, deleteClusterMembership, updateClusterName, upsertCluster, upsertClusterMembership } from "@/lib/db/clusters";
import { clearPassportCluster, updatePassportCluster } from "@/lib/db/passports";
import type { ClusterMembershipRole } from "@/types/passport";

function text(formData: FormData, key: string) {
  const value = formData.get(key);
  return typeof value === "string" ? value.trim() : "";
}

function membershipRole(value: string): ClusterMembershipRole {
  return value === "clusterAdmin" ? "clusterAdmin" : "member";
}

export async function createClusterAction(formData: FormData) {
  await requireRole("admin");
  const now = new Date().toISOString();
  const name = text(formData, "name");
  if (!name) {
    throw new Error("Cluster name is required");
  }
  const clusterId = text(formData, "clusterId") || `cluster-${randomUUID()}`;
  await upsertCluster({
    clusterId,
    name,
    createdAt: now,
    updatedAt: now,
  });
  revalidatePath("/admin/clusters");
  revalidatePath("/registry");
}

export async function updateClusterAction(formData: FormData) {
  await requireRole("admin");
  const clusterId = text(formData, "clusterId");
  const name = text(formData, "name");
  if (!clusterId || !name) {
    throw new Error("clusterId and name are required");
  }
  await updateClusterName(clusterId, name);
  revalidatePath("/admin/clusters");
  revalidatePath("/registry");
}

export async function deleteClusterAction(formData: FormData) {
  await requireRole("admin");
  const clusterId = text(formData, "clusterId");
  if (!clusterId) {
    throw new Error("clusterId is required");
  }
  await clearPassportCluster(clusterId);
  await deleteCluster(clusterId);
  revalidatePath("/admin/clusters");
  revalidatePath("/admin/passports");
  revalidatePath("/registry");
}

export async function assignPassportClusterAction(formData: FormData) {
  await requireRole("admin");
  const passportId = text(formData, "passportId");
  const clusterId = text(formData, "clusterId");
  if (!passportId || !clusterId) {
    throw new Error("passportId and clusterId are required");
  }
  await updatePassportCluster(passportId, clusterId);
  revalidatePath("/admin/clusters");
  revalidatePath("/admin/passports");
  revalidatePath("/registry");
}

export async function assignUserMembershipAction(formData: FormData) {
  await requireRole("admin");
  const email = text(formData, "email").toLowerCase();
  const clusterId = text(formData, "clusterId");
  if (!email || !clusterId) {
    throw new Error("email and clusterId are required");
  }
  await upsertClusterMembership({
    email,
    clusterId,
    role: membershipRole(text(formData, "role")),
  });
  revalidatePath("/admin/clusters");
  revalidatePath("/registry");
}

export async function deleteUserMembershipAction(formData: FormData) {
  await requireRole("admin");
  const email = text(formData, "email").toLowerCase();
  const clusterId = text(formData, "clusterId");
  if (!email || !clusterId) {
    throw new Error("email and clusterId are required");
  }
  await deleteClusterMembership(email, clusterId);
  revalidatePath("/admin/clusters");
  revalidatePath("/registry");
}
