import { createApp } from "../server/app.mjs";

// Vercel owns the HTTP server; export the handler without opening a port.
const app = createApp();
// Vercel supplies the forwarded client address through its reverse proxy.
app.set("trust proxy", 1);

export default app;
