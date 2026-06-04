# AWS Elastic Beanstalk Deploy Runbook

This runbook deploys the current ASP.NET app to AWS Elastic Beanstalk while keeping the existing MongoDB Atlas database and data.

Target shape:

```text
User -> sib-dpd.eu -> Route 53 -> Elastic Beanstalk load balancer -> app instance -> NAT Gateway fixed IP -> MongoDB Atlas
```

Do not run seed/reset during deployment. Use the same `MONGODB_URI` and `MONGODB_DB` you use now.

## 1. Build the upload zip

From the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\publish-elastic-beanstalk.ps1
```

Upload the zip printed by the script, for example:

```text
artifacts\releases\BatteryPassWeb-elastic-beanstalk-YYYYMMDD-HHMMSS.zip
```

The script adds the Elastic Beanstalk `Procfile` and removes `.env*` files from the package.

## 2. Create the AWS network first

You are currently in the AWS Elastic Beanstalk console in `eu-north-1`.

Use the same region for VPC, Elastic Beanstalk, ACM, and the load balancer. `eu-north-1` is fine. If `.NET 10` is not available in that region in the Beanstalk platform list, switch to `eu-central-1` and do all steps there instead.

1. In AWS search, type `VPC`.
2. Open **VPC**.
3. Click **Create VPC**.
4. Choose **VPC and more**.
5. Name: `battery-pass-vpc`.
6. IPv4 CIDR: `10.0.0.0/16`.
7. Availability Zones: `2`.
8. Public subnets: `2`.
9. Private subnets: `2`.
10. NAT gateways: **1 NAT gateway**.
11. VPC endpoints: **None**.
12. Click **Create VPC**.

After it finishes:

1. In the VPC sidebar, click **NAT gateways**.
2. Click the NAT gateway for `battery-pass-vpc`.
3. Copy the **Elastic IP address**. This is the only AWS IP MongoDB Atlas should allow.

## 3. Allow only AWS in MongoDB Atlas

In MongoDB Atlas:

1. Open your Atlas project.
2. Go to **Security** -> **Network Access**.
3. Click **Add IP Address**.
4. Paste the NAT Gateway Elastic IP from AWS.
5. Make it `/32`, like:

   ```text
   12.34.56.78/32
   ```

6. Comment: `AWS Elastic Beanstalk NAT`.
7. Save.

Do not add `0.0.0.0/0`.

You may keep your own home/office IP in Atlas if you still want to run the app locally from your PC.

## 4. Create Elastic Beanstalk

Go back to your current page:

```text
Elastic Beanstalk -> Create application
```

Use these values:

1. Environment tier: **Web server environment**.
2. Application name: `battery-pass`.
3. Environment name: `battery-pass-prod`.
4. Platform: **.NET**.
5. Platform branch: **.NET 10 on Amazon Linux 2023**.
6. Application code: **Upload your code**.
7. Upload the zip from `artifacts\releases`.
8. Preset: **Custom configuration**.

If AWS asks for roles:

1. Service role: let AWS create or use `aws-elasticbeanstalk-service-role`.
2. EC2 instance profile: let AWS create or use `aws-elasticbeanstalk-ec2-role`.

## 5. Configure Beanstalk networking

In the Beanstalk create wizard:

1. VPC: choose `battery-pass-vpc`.
2. Instance subnets: choose the **private** subnets.
3. Load balancer subnets: choose the **public** subnets.
4. Public IP address for instances: **disabled** or **unchecked**.
5. Environment type: **Load balanced**.
6. Load balancer type: **Application Load Balancer**.
7. Min instances: `1`.
8. Max instances: `1`.
9. Instance type: start with `t3.micro` or `t3.small`.
10. Health check path: `/`.

## 6. Add environment variables

Add these in Elastic Beanstalk **Environment properties**.

For the first temporary AWS URL test:

```text
ASPNETCORE_ENVIRONMENT=Production
REQUIRE_HTTPS_REDIRECTION=false
MONGODB_URI=<your existing MongoDB Atlas connection string>
MONGODB_DB=<your existing database name>
SESSION_SECRET=<long random value>
EXTERNAL_API_ENCRYPTION_KEY=<32 character random value>
ID_GENERATION_SECRET=<long random value>
DEMO_ADMIN_EMAIL=<your real admin email>
DEMO_ADMIN_PASSWORD=<strong password>
```

Optional if you use password reset emails:

```text
POWER_AUTOMATE_RESET_WEBHOOK_URL=<your existing webhook URL>
POWER_AUTOMATE_RESET_WEBHOOK_SECRET=<your existing webhook secret>
PASSWORD_RESET_APP_NAME=Battery Pass
```

Do not upload `.env.local`. Do not put secrets in Git.

## 7. Create and test the temporary public URL

Click **Create environment** or **Submit**.

Wait until the environment health is **Green**.

AWS will give you a temporary URL like:

```text
http://battery-pass-env.eba-2a34ep5k.eu-north-1.elasticbeanstalk.com
```

Open it and check the public homepage, registry pages, and `/help` API workbench.

While using this temporary Beanstalk URL, keep these environment properties:

```text
APP_BASE_URL=http://battery-pass-env.eba-2a34ep5k.eu-north-1.elasticbeanstalk.com
APP_URL=http://battery-pass-env.eba-2a34ep5k.eu-north-1.elasticbeanstalk.com
REQUIRE_HTTPS_REDIRECTION=false
```

The temporary external API base URL is:

```text
http://battery-pass-env.eba-2a34ep5k.eu-north-1.elasticbeanstalk.com/api/external/v1
```

The `/help` request workbench uses the current browser host, so when `/help` is opened on Beanstalk it sends API calls to Beanstalk. When the domain is later connected, the same workbench will use the domain automatically.

This URL is good for temporary testing. The real public URL should be `https://sib-dpd.eu` once the domain and HTTPS certificate are ready.

## 8. Create Route 53 hosted zone

In AWS:

1. Search `Route 53`.
2. Open **Route 53**.
3. Click **Hosted zones**.
4. Click **Create hosted zone**.
5. Domain name: `sib-dpd.eu`.
6. Type: **Public hosted zone**.
7. Click **Create hosted zone**.

Route 53 shows 4 nameservers.

At the registrar where `sib-dpd.eu` was bought:

1. Open domain settings.
2. Find **Nameservers**.
3. Choose **Custom nameservers**.
4. Paste the 4 Route 53 nameservers.
5. Save.

If the domain already has email, copy existing MX/TXT records into Route 53 before changing nameservers.

## 9. Request HTTPS certificate

In AWS, stay in the same region as Beanstalk, for example `eu-north-1`.

1. Search `Certificate Manager`.
2. Open **AWS Certificate Manager**.
3. Click **Request certificate**.
4. Choose **Request a public certificate**.
5. Add:

   ```text
   sib-dpd.eu
   www.sib-dpd.eu
   ```

6. Validation method: **DNS validation**.
7. Request.
8. Click **Create records in Route 53**.
9. Wait until status is **Issued**.

## 10. Add HTTPS to Beanstalk

In Elastic Beanstalk:

1. Open `battery-pass-prod`.
2. Go to **Configuration**.
3. Open **Load balancer**.
4. Click **Edit**.
5. Add listener:

   ```text
   Port: 443
   Protocol: HTTPS
   Certificate: sib-dpd.eu certificate
   ```

6. Save/apply.

## 11. Point sib-dpd.eu to Beanstalk

In Route 53 hosted zone `sib-dpd.eu`, create record 1:

```text
Record name: empty
Type: A
Alias: Yes
Route traffic to: Elastic Beanstalk environment
Region: eu-north-1
Environment: battery-pass-prod
```

Create record 2:

```text
Record name: www
Type: A
Alias: Yes
Route traffic to: Elastic Beanstalk environment
Region: eu-north-1
Environment: battery-pass-prod
```

Wait a few minutes, sometimes longer, then test:

```text
https://sib-dpd.eu
https://www.sib-dpd.eu
```

## 12. Turn HTTPS enforcement back on

After `https://sib-dpd.eu` works:

1. Go to Elastic Beanstalk.
2. Open `battery-pass-prod`.
3. Go to **Configuration**.
4. Open **Software** or **Environment properties**.
5. Change:

   ```text
   REQUIRE_HTTPS_REDIRECTION=true
   APP_BASE_URL=https://sib-dpd.eu
   APP_URL=https://sib-dpd.eu
   ```

6. Apply.
7. Test:

   ```text
   http://sib-dpd.eu
   ```

   It should redirect to:

   ```text
   https://sib-dpd.eu
   ```

## 13. What not to do

- Do not add MongoDB Atlas network access `0.0.0.0/0`.
- Do not upload `.env.local`.
- Do not run reset/seed unless you intentionally want to change database data.
- Do not log in over the temporary HTTP Beanstalk URL.
- Do not create the Beanstalk app in one region and the certificate in another region.

## References

- Elastic Beanstalk .NET Linux platform and `Procfile`: https://docs.aws.amazon.com/elasticbeanstalk/latest/dg/dotnet-linux-platform.html
- Elastic Beanstalk Procfile details: https://docs.aws.amazon.com/elasticbeanstalk/latest/dg/dotnet-linux-procfile.html
- Route 53 alias to Elastic Beanstalk: https://docs.aws.amazon.com/Route53/latest/DeveloperGuide/routing-to-beanstalk-environment.html
- Elastic Beanstalk HTTPS: https://docs.aws.amazon.com/elasticbeanstalk/latest/dg/configuring-https-elb.html
- MongoDB Atlas IP access list: https://www.mongodb.com/docs/atlas/security/ip-access-list/
