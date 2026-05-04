import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";

export function LoginForm({ error, nextPath = "" }: { error?: string; nextPath?: string }) {
  return (
    <form action={nextPath ? `/api/auth/login?next=${encodeURIComponent(nextPath)}` : "/api/auth/login"} method="post" className="space-y-4">
      {nextPath ? <input type="hidden" name="next" value={nextPath} /> : null}
      {error ? (
        <p className="rounded-md bg-red-50 px-3 py-2 text-sm text-red-700 dark:bg-red-950 dark:text-red-200">
          Invalid email or password.
        </p>
      ) : null}
      <label className="block text-sm">
        <span className="mb-1 block font-medium">Email / Username</span>
        <Input name="email" type="email" required placeholder="admin@example.test" className="w-full" />
      </label>
      <label className="block text-sm">
        <span className="mb-1 block font-medium">Password</span>
        <Input name="password" type="password" required placeholder="Password123!" className="w-full" />
      </label>
      <Button type="submit" className="w-full">
        Verify
      </Button>
    </form>
  );
}
