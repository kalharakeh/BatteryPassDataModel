# Legacy reference only

This Next.js app is retained as legacy/reference code for the Battery Pass demo.

Do not deploy this app publicly. The production/demo Elastic Beanstalk bundle is the ASP.NET app under `web/`, not this `web-next-legacy/` directory.

Before reviving or exposing this app, complete a separate upgrade pass:

```powershell
npm audit --omit=dev
npm install next@16.2.9 eslint-config-next@16.2.9
npm audit --omit=dev
npm run build
```

Only deploy it after the production dependency audit is clean and the app has passed a fresh security review.
