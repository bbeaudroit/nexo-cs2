// One atomic private ledger per capability: ETags serialize all config writes.
export function createPrivateSyncStore({
  token = process.env.BLOB_READ_WRITE_TOKEN,
  storeId = process.env.BLOB_STORE_ID,
  sdk,
} = {}) {
  if (!token && !storeId) return null;
  const auth = token ? { token } : { storeId };
  const client = async () => sdk || (await import("@vercel/blob"));
  const key = (namespace) => {
    if (!/^[a-f0-9]{64}$/.test(namespace)) throw Error("Invalid namespace");
    return `private-sync/v1/${namespace}.json`;
  };
  return {
    async read(namespace) {
      const api = await client();
      const result = await api.get(key(namespace), {
        ...auth,
        access: "private",
        useCache: false,
        // Compression weakens HTTP ETags, which cannot satisfy strong If-Match.
        headers: { "Accept-Encoding": "identity" },
      });
      if (!result) return null;
      if (result.statusCode !== 200 || !result.stream || !result.blob?.etag)
        throw Error("Private storage read failed");
      return {
        etag: result.blob.etag,
        value: JSON.parse(await new Response(result.stream).text()),
      };
    },
    async compareAndSwap(namespace, etag, value) {
      const api = await client();
      try {
        await api.put(key(namespace), JSON.stringify(value), {
          ...auth,
          access: "private",
          contentType: "application/json",
          addRandomSuffix: false,
          allowOverwrite: etag !== null,
          ...(etag !== null ? { ifMatch: etag } : {}),
        });
        return true;
      } catch (error) {
        if (
          (api.BlobPreconditionFailedError &&
            error instanceof api.BlobPreconditionFailedError) ||
          (api.BlobAlreadyExistsError &&
            error instanceof api.BlobAlreadyExistsError)
        ) {
          return false;
        }
        throw error;
      }
    },
  };
}
