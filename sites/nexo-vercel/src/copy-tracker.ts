// Keep the initial Set-Cookie response ahead of any subsequent copy request.
// Web Locks extend this ordering across tabs on browsers that support them.
export function createCopyTracker(
  lock?: (job: () => Promise<void>) => Promise<void>,
) {
  let tail: Promise<void> = Promise.resolve();
  return (job: () => Promise<void>) => {
    const next = tail.then(() => (lock ? lock(job) : job()));
    tail = next.catch(() => {});
    return next;
  };
}
