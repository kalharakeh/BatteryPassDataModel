import { LoginForm } from "@/components/auth/login-form";
import { Card } from "@/components/ui/card";

export default async function LoginPage({ searchParams }: { searchParams: Promise<{ error?: string; next?: string }> }) {
  const params = await searchParams;
  return (
    <main className="mx-auto flex min-h-[70vh] max-w-md items-center px-4 py-12">
      <Card className="w-full">
        <h1 className="text-xl font-semibold">Authenticate for privileged access</h1>
        <p className="mt-2 text-sm text-slate-600 dark:text-slate-300">
          Sign in with demo credentials to view admin and privileged sections.
        </p>
        <div className="mt-6">
          <LoginForm error={params.error} nextPath={params.next ?? ""} />
        </div>
      </Card>
    </main>
  );
}
