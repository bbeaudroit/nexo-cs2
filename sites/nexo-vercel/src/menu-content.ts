export const features = [
  {
    id: "esp",
    title: "ESP & HUD",
    icon: "eye",
    summary: "Choose what you see.",
    description:
      "Show player boxes, skeletons, health and weapon names. Pick the colors and move labels where you want them.",
    notes: [
      "Teammates stay hidden unless you turn them on.",
      "Set a distance limit and adjust the overlay FPS limit.",
    ],
    image: "visuals",
    module: "visuals",
    tab: "players",
  },
  {
    id: "aim",
    title: "Aimbot",
    icon: "crosshair",
    summary: "Set aim by weapon.",
    description:
      "Choose an aim key, the area around your crosshair and how smoothly the aim moves. Nexo switches to the settings for your current weapon.",
    notes: [
      "FOV is the area where aim can select a target.",
      "Sensitivity can follow your saved CS2 settings.",
      "Camera reading for CS2 build 14193 has been revalidated in the development build. It is not included in the current 0.7.3 download.",
    ],
    image: "aimbot",
    module: "aim",
  },
  {
    id: "trigger",
    title: "Trigger",
    icon: "mouse-pointer-2",
    summary: "Control when it fires.",
    description:
      "Set a key to fire when a player is under the crosshair. Choose the delay, time between shots and maximum distance.",
    notes: [
      "Separate settings for each weapon or weapon group.",
      "Off by default.",
    ],
    image: "trigger",
    module: "trigger",
  },
  {
    id: "radar",
    title: "Radar",
    icon: "radar",
    summary: "Keep track of the map.",
    description:
      "Position the local radar by dragging its preview or entering numbers. Share a separate web radar with a private link.",
    notes: [
      "The shared radar shows the full map without rotating.",
      "Auto map detection, manual map selection and floor controls where available.",
    ],
    image: "radar",
    module: "radar",
  },
  {
    id: "chams",
    title: "Chams & history",
    icon: "layers",
    summary: "See a player silhouette.",
    description:
      "Adjust the silhouette color, outline and opacity. History can show one recent pose behind a moving player.",
    notes: [
      "Uses an external SAS model with an approximate pose. Wallbang colors are not available yet.",
      "History is off by default and has a rendering limit.",
    ],
    image: "visuals",
    module: "visuals",
    tab: "history",
  },
  {
    id: "movement",
    title: "Movement",
    icon: "footprints",
    summary: "Set your movement keys.",
    description:
      "Hold your chosen key for bunny hop. Mouse-steered auto-strafe changes A/D according to your turn while airborne.",
    notes: [
      "Your manual movement takes priority. Both features start off.",
      "Match the jump key to the bind used in CS2.",
    ],
    image: "movement",
    module: "misc",
    tab: "movement",
  },
  {
    id: "cfg",
    title: "CFG",
    icon: "sliders-horizontal",
    summary: "Save it. Share it.",
    description:
      "Save your setup and share it as a NEXO1 code. The menu shows the CFG name and declared author before you apply it.",
    notes: [
      "Import and export from the menu. Installed updates keep your saved CFGs.",
      "Private sync links your devices with a secret code. Uploads, downloads and replacements need your action.",
      "Browse community CFGs on their own page.",
      "A skin loadout library is in development. It is not included in the current 0.7.3 download; in-game application is pending the CS2 update.",
    ],
    image: "cfg",
    module: "configs",
  },
  {
    id: "streamproof",
    title: "Streamproof",
    icon: "scan-eye",
    summary: "Choose what gets recorded.",
    description:
      "Exclude the menu and overlay from supported Windows recording methods. A hold-to-hide key can temporarily hide the overlay.",
    notes: [
      "Check your actual OBS or recording source first.",
      "Use windowed or borderless mode.",
    ],
    image: "visuals",
    module: "system",
    tab: "capture",
  },
];
export const shots = [
  { id: "visuals", label: "Visuals", caption: "Player ESP and model preview." },
  { id: "aimbot", label: "Aimbot", caption: "Aim settings for each weapon." },
  {
    id: "trigger",
    label: "Trigger",
    caption: "Firing delay, keys and distance.",
  },
  {
    id: "radar",
    label: "Radar",
    caption: "Position the radar in a screen preview.",
  },
  {
    id: "movement",
    label: "Movement",
    caption: "Bunny hop, auto-strafe and keybinds.",
  },
  { id: "cfg", label: "CFG", caption: "Generate, inspect and import a code." },
  { id: "updates", label: "Updates", caption: "Check releases, install and restart, or restore a saved backup." },
  { id: "private-sync", label: "Private sync", caption: "Keep private CFGs on linked devices. Anyone with the pairing code can access the vault." },
];
export const tags = [
  "legit",
  "rage",
  "rifles",
  "pistols",
  "snipers",
  "visuals",
  "movement",
  "low-end",
];

