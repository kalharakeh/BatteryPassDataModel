"use server";

import bcrypt from "bcryptjs";
import { revalidatePath } from "next/cache";
import { requireClusterAdminForCluster } from "@/lib/auth/cluster-guards";
import { deleteClusterMembership, upsertClusterMembership } from "@/lib/db/clusters";
import { userCollection } from "@/lib/db/collections";
import type { ClusterMembershipRole } from "@/types/passport";

function text(formData: FormData, key: string) {
  const value = formData.get(key);
  return typeof value === "string" ? value.trim() : "";
}

function membershipRole(value: string): ClusterMembershipRole {
  return value === "clusterAdmin" ? "clusterAdmin" : "member";
}

function viewerRoles(existingRoles?: string[]) {
  const roles = existingRoles?.filter((role) => role !== "admin") ?? [];
  return roles.length ? roles : ["viewer"];
}

export async function saveClusterAdminUserAction(formData: FormData) {
  const email = text(formData, "email").toLowerCase();
  const name = text(formData, "name");
  const password = text(formData, "password");
  const clusterId = text(formData, "clusterId");
  if (!email || !clusterId) {
    throw new Error("email and clusterId are required");
  }
  await requireClusterAdminForCluster(clusterId);

  const users = await userCollection();
  const existingUser = await users.findOne({ email });
  if (existingUser?.roles?.includes("admin")) {
    throw new Error("Local admins cannot edit global admin users");
  }
  if (!existingUser && !password) {
    throw new Error("Password is required when adding a new user");
  }

  const update: Record<string, unknown> = {
    email,
    roles: viewerRoles(existingUser?.roles),
  };
  if (name || !existingUser) {
    update.name = name || email;
  }
  if (password) {
    update.passwordHash = await bcrypt.hash(password, 10);
  }

  await users.updateOne({ email }, { $set: update }, { upsert: true });
  await upsertClusterMembership({
    email,
    clusterId,
    role: membershipRole(text(formData, "role")),
  });
  revalidatePath("/cluster-admin");
  revalidatePath("/cluster-admin/users");
  revalidatePath("/registry");
}

export async function deleteClusterAdminUserMembershipAction(formData: FormData) {
  const email = text(formData, "email").toLowerCase();
  const clusterId = text(formData, "clusterId");
  if (!email || !clusterId) {
    throw new Error("email and clusterId are required");
  }
  await requireClusterAdminForCluster(clusterId);
  await deleteClusterMembership(email, clusterId);
  revalidatePath("/cluster-admin");
  revalidatePath("/cluster-admin/users");
  revalidatePath("/registry");
}
