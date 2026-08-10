const json = (body, status = 200) =>
  new Response(JSON.stringify(body), {
    status,
    headers: {
      "content-type": "application/json; charset=utf-8",
      "cache-control": "no-store",
      "x-content-type-options": "nosniff",
    },
  });

const emailPattern = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

export default {
  async fetch(request, env) {
    const url = new URL(request.url);

    if (url.pathname === "/api/subscribe") {
      if (request.method !== "POST") {
        return json({ message: "Method not allowed." }, 405);
      }

      let input;
      try {
        input = await request.json();
      } catch {
        return json({ message: "Invalid request." }, 400);
      }

      // Honeypot. Real users never see or fill this field.
      if (String(input.company ?? "").trim()) {
        return json({ message: "You’re on the list." });
      }

      const email = String(input.email ?? "").trim().toLowerCase();
      const site = String(input.site ?? "").trim().toLowerCase();

      if (!email || email.length > 254 || !emailPattern.test(email)) {
        return json({ message: "Enter a valid email address." }, 400);
      }

      if (!["corporate", "app"].includes(site)) {
        return json({ message: "Invalid site." }, 400);
      }

      try {
        await env.DB.prepare(`
          INSERT INTO launch_signups (email, site, created_at, updated_at)
          VALUES (?, ?, datetime('now'), datetime('now'))
          ON CONFLICT(email, site) DO UPDATE SET
            updated_at = datetime('now')
        `).bind(email, site).run();

        return json({ message: "You’re on the list. We’ll let you know when we launch." });
      } catch (error) {
        console.error("Signup insert failed", error);
        return json({ message: "Could not save your email right now. Please try again." }, 500);
      }
    }

    return env.ASSETS.fetch(request);
  },
};
