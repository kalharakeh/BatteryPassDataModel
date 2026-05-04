import Link from "next/link";
import { Save, Trash2, UserPlus } from "lucide-react";
import { Card } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { listClusterAdminUserMemberships } from "@/lib/auth/cluster-guards";
import { listClusters } from "@/lib/db/clusters";
import { userCollection } from "@/lib/db/collections";
import type { Cluster, ClusterMembership } from "@/types/passport";
import { deleteClusterAdminUserMembershipAction, saveClusterAdminUserAction } from "./actions";

export const dynamic = "force-dynamic";

function Select({
  name,
  children,
  defaultValue,
  className = "",
}: {
  name: string;
  children: React.ReactNode;
  defaultValue?: string;
  className?: string;
}) {
  return (
    <select
      name={name}
      defaultValue={defaultValue}
      className={`min-h-10 rounded-md border border-slate-300 bg-white px-3 text-sm outline-none focus:border-emerald-600 dark:border-slate-700 dark:bg-slate-950 ${className}`}
    >
      {children}
    </select>
  );
}

function roleLabel(role: ClusterMembership["role"]) {
  return role === "clusterAdmin" ? "Local admin" : "Normal user";
}

function clusterLabel(clusterId: string, clustersById: Map<string, Cluster>) {
  const cluster = clustersById.get(clusterId);
  return cluster ? `${cluster.name} (${cluster.clusterId})` : clusterId;
}

function SaveButton({ children }: { children: React.ReactNode }) {
  return (
    <button
      className="inline-flex min-h-10 items-center justify-center gap-2 rounded-md border border-slate-300 px-3 text-sm font-medium text-slate-700 hover:border-emerald-600 dark:border-slate-700 dark:text-slate-100"
      type="submit"
    >
      {children}
    </button>
  );
}

export default async function ClusterAdminUsersPage() {
  const [{ administeredClusterIds, memberships }, clusters, usersCollection] = await Promise.all([
    listClusterAdminUserMemberships(),
    listClusters(),
    userCollection(),
  ]);
  const administeredClusters = clusters.filter((cluster) => administeredClusterIds.includes(cluster.clusterId));
  const clustersById = new Map(clusters.map((cluster) => [cluster.clusterId, cluster]));
  const visibleEmails = [...new Set(memberships.map((membership) => membership.email))];
  const users = visibleEmails.length
    ? ((await usersCollection
        .find({ email: { $in: visibleEmails } }, { projection: { _id: 0, email: 1, name: 1, roles: 1 } })
        .sort({ email: 1 })
        .toArray()) as Array<{ email: string; name?: string; roles?: string[] }>)
    : [];
  const usersByEmail = new Map(users.map((user) => [user.email, user]));
  const membershipsByUser = new Map<string, ClusterMembership[]>();
  for (const membership of memberships) {
    const userMemberships = membershipsByUser.get(membership.email) ?? [];
    userMemberships.push(membership);
    membershipsByUser.set(membership.email, userMemberships);
  }
  const userRows = Array.from(membershipsByUser.entries()).sort(([emailA], [emailB]) => emailA.localeCompare(emailB));

  return (
    <main className="mx-auto max-w-7xl px-4 py-10">
      <div className="flex flex-wrap items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-semibold">Cluster users</h1>
          <p className="mt-2 text-sm text-slate-600 dark:text-slate-300">
            Add, edit, or remove users only for clusters where you are a local admin.
          </p>
        </div>
        <Link
          href="/cluster-admin"
          className="inline-flex min-h-10 items-center justify-center rounded-md border border-slate-300 px-4 text-sm font-medium hover:border-emerald-600 dark:border-slate-700"
        >
          Cluster admin
        </Link>
      </div>

      <div className="mt-6 grid gap-6 xl:grid-cols-[360px_1fr]">
        <Card>
          <h2 className="flex items-center gap-2 font-semibold">
            <UserPlus className="h-4 w-4" />
            Add or update user
          </h2>
          <form action={saveClusterAdminUserAction} className="mt-4 grid gap-3">
            <Input name="email" placeholder="user@example.test" type="email" required />
            <Input name="name" placeholder="Display name" />
            <Input name="password" placeholder="Password for new users or reset" type="password" />
            <Select name="clusterId">
              {administeredClusters.map((cluster) => (
                <option key={cluster.clusterId} value={cluster.clusterId}>
                  {cluster.name}
                </option>
              ))}
            </Select>
            <Select name="role" defaultValue="member">
              <option value="member">Normal user</option>
              <option value="clusterAdmin">Local admin</option>
            </Select>
            <button className="inline-flex min-h-10 items-center justify-center gap-2 rounded-md bg-emerald-700 px-4 text-sm font-medium text-white hover:bg-emerald-800" type="submit">
              <Save className="h-4 w-4" />
              Save user
            </button>
          </form>
        </Card>

        <Card>
          <h2 className="font-semibold">Users in managed clusters</h2>
          <div className="mt-4 overflow-x-auto">
            <table className="w-full min-w-[900px] text-left text-sm">
              <thead className="border-b border-slate-200 text-xs uppercase text-slate-500 dark:border-slate-800">
                <tr>
                  <th className="py-2 pr-4">User</th>
                  <th className="py-2 pr-4">Managed cluster memberships</th>
                  <th className="py-2 pr-4">Edit</th>
                </tr>
              </thead>
              <tbody>
                {userRows.length ? (
                  userRows.map(([email, userMemberships]) => {
                    const user = usersByEmail.get(email);
                    return (
                      <tr key={email} className="border-b border-slate-100 align-top last:border-0 dark:border-slate-800">
                        <td className="py-3 pr-4">
                          <p className="font-medium">{email}</p>
                          {user?.name ? <p className="text-xs text-slate-500">{user.name}</p> : null}
                          {user?.roles?.length ? <p className="text-xs text-slate-500">{user.roles.join(", ")}</p> : null}
                        </td>
                        <td className="py-3 pr-4">
                          <div className="flex flex-wrap gap-2">
                            {userMemberships.map((membership) => (
                              <form
                                key={`${membership.email}-${membership.clusterId}`}
                                action={deleteClusterAdminUserMembershipAction}
                                className="inline-flex min-h-9 items-center gap-2 rounded-md border border-slate-200 px-2 text-xs dark:border-slate-800"
                              >
                                <input type="hidden" name="email" value={membership.email} />
                                <input type="hidden" name="clusterId" value={membership.clusterId} />
                                <span>{clusterLabel(membership.clusterId, clustersById)}</span>
                                <span className="rounded-full bg-slate-100 px-2 py-0.5 font-medium dark:bg-slate-800">{roleLabel(membership.role)}</span>
                                <button className="inline-flex text-red-700 hover:text-red-800 dark:text-red-300" type="submit" aria-label={`Remove ${membership.email} from ${membership.clusterId}`}>
                                  <Trash2 className="h-3.5 w-3.5" />
                                </button>
                              </form>
                            ))}
                          </div>
                        </td>
                        <td className="py-3 pr-4">
                          <form action={saveClusterAdminUserAction} className="flex flex-wrap gap-2">
                            <input type="hidden" name="email" value={email} />
                            <Input name="name" defaultValue={user?.name ?? ""} placeholder="Display name" />
                            <Input name="password" placeholder="New password" type="password" />
                            <Select name="clusterId" className="min-w-52">
                              {administeredClusters.map((cluster) => (
                                <option key={cluster.clusterId} value={cluster.clusterId}>
                                  {cluster.name}
                                </option>
                              ))}
                            </Select>
                            <Select name="role" defaultValue="member" className="min-w-36">
                              <option value="member">Normal user</option>
                              <option value="clusterAdmin">Local admin</option>
                            </Select>
                            <SaveButton>
                              <Save className="h-4 w-4" />
                              Save
                            </SaveButton>
                          </form>
                        </td>
                      </tr>
                    );
                  })
                ) : (
                  <tr>
                    <td className="py-4 text-sm text-slate-600 dark:text-slate-300" colSpan={3}>
                      No users are connected to your managed clusters yet.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </Card>
      </div>
    </main>
  );
}
