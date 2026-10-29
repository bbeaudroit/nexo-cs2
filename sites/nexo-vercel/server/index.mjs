import { createApp, readConfig } from "./app.mjs";
import { createServer } from "node:http";
const config = readConfig();
const server = createServer(createApp({ config }));
server.on("error", (error) => {
  console.error(`Nexo could not start: ${error.code || "server error"}.`);
  process.exitCode = 1;
});
server.listen(
  Number(process.env.PORT || 3000),
  process.env.HOST || "0.0.0.0",
  () => {
    console.log(
      `Nexo server started on port ${process.env.PORT || 3000}. Download: ${config.downloadUrl ? "configured" : "pending"}. Discord: ${config.webhookUrl ? "configured" : "pending"}.`,
    );
  },
);
