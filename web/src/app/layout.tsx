import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "Battery Passport - Viewer",
  description: "Internal Battery Pass demonstrator clone",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="en">
      <body>{children}</body>
    </html>
  );
}
