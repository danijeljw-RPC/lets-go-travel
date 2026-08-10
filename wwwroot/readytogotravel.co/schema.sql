CREATE TABLE IF NOT EXISTS launch_signups (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  email TEXT NOT NULL COLLATE NOCASE,
  site TEXT NOT NULL CHECK (site IN ('corporate', 'app')),
  created_at TEXT NOT NULL DEFAULT (datetime('now')),
  updated_at TEXT NOT NULL DEFAULT (datetime('now')),
  UNIQUE (email, site)
);

CREATE INDEX IF NOT EXISTS ix_launch_signups_site_created
  ON launch_signups (site, created_at);
