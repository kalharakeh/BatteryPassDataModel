import Link from "next/link";
import { Database, Layers, Plus, Save, Trash2, UsersRound } from "lucide-react";
import {
  assignPassportClusterAction,
  assignUserMembershipAction,
  createClusterAction,
  deleteClusterAction,
  deleteUserMembershipAction,
  updateClusterAction,
} from "./actions";
import { requireRole } from "@/lib/auth/session";
import { Card } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { listClusters, listClusterMemberships } from "@/lib/db/clusters";
import { listPassports } from "@/lib/db/passports";
import { userCollection } from "@/lib/db/collections";
import { toPassportViewModel } from "@/lib/view-model/passport-view-model";
import type { Cluster, ClusterMembership } from "@/types/passport";

export const dynamic = "force-dynamic";

type TabKey = "battery" | "clusters" | "users";

const tabs: Array<{ key: TabKey; label: string; icon: React.ReactNode }> = [
  { key: "battery", label: "Battery cluster assignments", icon: <Layers className="h-4 w-4" /> },
  { key: "clusters", label: "Registered clusters", icon: <Database className="h-4 w-4" /> },
  { key: "users", label: "Users", icon: <UsersRound className="h-4 w-4" /> },
];

function activeTab(tab?: string): TabKey {
  return tab === "clusters" || tab === "users" ? tab : "battery";
}

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

function SubmitButton({
  children,
  tone = "primary",
}: {
  children: React.ReactNode;
  tone?: "primary" | "secondary" | "danger";
}) {
  const styles = {
    primary: "bg-emerald-700 text-white hover:bg-emerald-800",
    secondary: "border border-slate-300 text-slate-700 hover:border-emerald-600 dark:border-slate-700 dark:text-slate-100",
    danger: "border border-red-200 text-red-700 hover:border-red-500 dark:border-red-900/70 dark:text-red-300",
  };

  return (
    <button className={`inline-flex min-h-10 items-center justify-center gap-2 rounded-md px-3 text-sm font-medium ${styles[tone]}`} type="submit">
      {children}
    </button>
  );
}

export default async function AdminClustersPage({ searchParams }: { searchParams: Promise<{ tab?: string }> }) {
  await requireRole("admin");
  const params = await searchParams;
  const selectedTab = activeTab(params.tab);
  const [clusters, memberships, passports, usersCollection] = await Promise.all([
    listClusters(),
    listClusterMemberships(),
    listPassports("", { includeArchived: true }),
    userCollection(),
  ]);
  const users = (await usersCollection
    .find({}, { projection: { _id: 0, email: 1, name: 1, roles: 1 } })
    .sort({ email: 1 })
    .toArray()) as Array<{ email: string; name?: string; roles?: string[] }>;
  const clustersById = new Map(clusters.map((cluster) => [cluster.clusterId, cluster]));
  const membershipsByUser = new Map<string, ClusterMembership[]>();
  for (const user of users) {
    membershipsByUser.set(String(user.email), []);
  }
  for (const membership of memberships) {
    const userMemberships = membershipsByUser.get(membership.email) ?? [];
    userMemberships.push(membership);
    membershipsByUser.set(membership.email, userMemberships);
  }
  const usersByEmail = new Map(users.map((user) => [String(user.email), user]));
  const userRows = Array.from(membershipsByUser.entries()).sort(([emailA], [emailB]) => emailA.localeCompare(emailB));

  return (
    <main className="mx-auto max-w-7xl px-4 py-10">
      <div>
        <h1 className="text-2xl font-semibold">Cluster administration</h1>
        <p className="mt-2 text-sm text-slate-600 dark:text-slate-300">
          General admins can assign batteries, maintain cluster records, and manage user memberships from one place.
        </p>
      </div>

      <nav className="mt-6 flex flex-wrap gap-2 border-b border-slate-200 pb-2 text-sm dark:border-slate-800" aria-label="Cluster administration tabs">
        {tabs.map((tab) => {
          const isActive = tab.key === selectedTab;
          return (
            <Link
              key={tab.key}
              href={`/admin/clusters?tab=${tab.key}`}
              aria-current={isActive ? "page" : undefined}
              className={`inline-flex min-h-10 items-center gap-2 rounded-md px-3 font-medium ${
                isActive
                  ? "border border-emerald-500 bg-emerald-100 text-emerald-950 shadow-sm dark:border-emerald-300 dark:bg-emerald-200 dark:text-emerald-950"
                  : "text-slate-600 hover:bg-slate-100 dark:text-slate-300 dark:hover:bg-slate-900"
              }`}
            >
              {tab.icon}
              {tab.label}
            </Link>
          );
        })}
      </nav>

      {selectedTab === "battery" ? (
        <Card className="mt-6">
          <h2 className="font-semibold">Battery cluster assignments</h2>
          <div className="mt-4 overflow-x-auto">
            <table className="w-full min-w-[820px] text-left text-sm">
              <thead className="border-b border-slate-200 text-xs uppercase text-slate-500 dark:border-slate-800">
                <tr>
                  <th className="py-2 pr-4">Battery</th>
                  <th className="py-2 pr-4">Current cluster</th>
                  <th className="py-2 pr-4">Assign</th>
                </tr>
              </thead>
              <tbody>
                {passports.map((passport) => {
                  const viewModel = toPassportViewModel(passport, { clusters });
                  return (
                    <tr key={passport.passportId} className="border-b border-slate-100 last:border-0 dark:border-slate-800">
                      <td className="py-3 pr-4">
                        <p className="font-medium">{viewModel.modelNumber}</p>
                        <p className="break-all text-xs text-slate-500">{passport.passportId}</p>
                      </td>
                      <td className="py-3 pr-4">
                        <p>{viewModel.clusterLabel}</p>
                        {passport.clusterId ? <p className="text-xs text-slate-500">{passport.clusterId}</p> : null}
                      </td>
                      <td className="py-3 pr-4">
                        <form action={assignPassportClusterAction} className="flex flex-wrap gap-2">
                          <input type="hidden" name="passportId" value={passport.passportId} />
                          <Select name="clusterId" defaultValue={passport.clusterId ?? clusters[0]?.clusterId} className="min-w-56">
                            {clusters.map((cluster) => (
                              <option key={cluster.clusterId} value={cluster.clusterId}>
                                {cluster.name}
                              </option>
                            ))}
                          </Select>
                          <SubmitButton tone="secondary">
                            <Save className="h-4 w-4" />
                            Save
                          </SubmitButton>
                        </form>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </Card>
      ) : null}

      {selectedTab === "clusters" ? (
        <div className="mt-6 grid gap-6 xl:grid-cols-[360px_1fr]">
          <Card>
            <h2 className="flex items-center gap-2 font-semibold">
              <Plus className="h-4 w-4" />
              Add cluster
            </h2>
            <form action={createClusterAction} className="mt-4 grid gap-3">
              <Input name="name" placeholder="North Operations Cluster" required />
              <Input name="clusterId" placeholder="Optional cluster ID" />
              <SubmitButton>
                <Save className="h-4 w-4" />
                Save cluster
              </SubmitButton>
            </form>
          </Card>

          <Card>
            <h2 className="font-semibold">Registered clusters</h2>
            <div className="mt-4 overflow-x-auto">
              <table className="w-full min-w-[720px] text-left text-sm">
                <thead className="border-b border-slate-200 text-xs uppercase text-slate-500 dark:border-slate-800">
                  <tr>
                    <th className="py-2 pr-4">Cluster ID</th>
                    <th className="py-2 pr-4">Name</th>
                    <th className="py-2 pr-4">Actions</th>
                  </tr>
                </thead>
                <tbody>
                  {clusters.map((cluster) => (
                    <tr key={cluster.clusterId} className="border-b border-slate-100 last:border-0 dark:border-slate-800">
                      <td className="py-3 pr-4 font-mono text-xs">{cluster.clusterId}</td>
                      <td className="py-3 pr-4">
                        <form action={updateClusterAction} className="flex flex-wrap gap-2">
                          <input type="hidden" name="clusterId" value={cluster.clusterId} />
                          <Input name="name" defaultValue={cluster.name} required />
                          <SubmitButton tone="secondary">
                            <Save className="h-4 w-4" />
                            Save
                          </SubmitButton>
                        </form>
                      </td>
                      <td className="py-3 pr-4">
                        <form action={deleteClusterAction}>
                          <input type="hidden" name="clusterId" value={cluster.clusterId} />
                          <SubmitButton tone="danger">
                            <Trash2 className="h-4 w-4" />
                            Delete
                          </SubmitButton>
                        </form>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </Card>
        </div>
      ) : null}

      {selectedTab === "users" ? (
        <Card className="mt-6">
          <h2 className="font-semibold">Users</h2>
          <div className="mt-4 overflow-x-auto">
            <table className="w-full min-w-[960px] text-left text-sm">
              <thead className="border-b border-slate-200 text-xs uppercase text-slate-500 dark:border-slate-800">
                <tr>
                  <th className="py-2 pr-4">User</th>
                  <th className="py-2 pr-4">Clusters and roles</th>
                  <th className="py-2 pr-4">Add or modify membership</th>
                </tr>
              </thead>
              <tbody>
                {userRows.map(([email, userMemberships]) => {
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
                          {userMemberships.length ? (
                            userMemberships.map((membership) => (
                              <form
                                key={`${membership.email}-${membership.clusterId}`}
                                action={deleteUserMembershipAction}
                                className="inline-flex min-h-9 items-center gap-2 rounded-md border border-slate-200 px-2 text-xs dark:border-slate-800"
                              >
                                <input type="hidden" name="email" value={membership.email} />
                                <input type="hidden" name="clusterId" value={membership.clusterId} />
                                <span>{clusterLabel(membership.clusterId, clustersById)}</span>
                                <span className="rounded-full bg-slate-100 px-2 py-0.5 font-medium dark:bg-slate-800">{roleLabel(membership.role)}</span>
                                <button className="inline-flex text-red-700 hover:text-red-800 dark:text-red-300" type="submit" aria-label={`Delete ${membership.clusterId} membership`}>
                                  <Trash2 className="h-3.5 w-3.5" />
                                </button>
                              </form>
                            ))
                          ) : (
                            <span className="text-slate-500">No cluster membership</span>
                          )}
                        </div>
                      </td>
                      <td className="py-3 pr-4">
                        <form action={assignUserMembershipAction} className="flex flex-wrap gap-2">
                          <input type="hidden" name="email" value={email} />
                          <Select name="clusterId" className="min-w-56">
                            {clusters.map((cluster) => (
                              <option key={cluster.clusterId} value={cluster.clusterId}>
                                {cluster.name}
                              </option>
                            ))}
                          </Select>
                          <Select name="role" defaultValue="member" className="min-w-36">
                            <option value="member">Normal user</option>
                            <option value="clusterAdmin">Local admin</option>
                          </Select>
                          <SubmitButton tone="secondary">
                            <Save className="h-4 w-4" />
                            Save
                          </SubmitButton>
                        </form>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </Card>
      ) : null}
    </main>
  );
}
