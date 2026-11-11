namespace OpenFill.Core.Agent;

// OF-000033: the authenticated account link is rendered only while the
// LinkedIn "Me" menu is open in its current React navigation.
// One bounded, DOM-local read restores the menu's previous open/closed state.
public static class LinkedInAccountMenuProbe
{
    public const string Javascript = """
        (async () => {
          if (location.hostname !== "www.linkedin.com") return [];
          const legacy = document.querySelector(
            '.global-nav__me-content,.global-nav__me-menu,[data-test-global-nav-me-menu]');
          if (legacy) {
            const links = [...legacy.querySelectorAll('a[href*="/in/"]')]
              .slice(0,5).map(a => a.href);
            if (links.length) return links;
          }
          const nav = [...document.querySelectorAll('nav')].find(n =>
            !!n.querySelector('a[href*="/mynetwork"]') &&
            !!n.querySelector('a[href*="/messaging"]') &&
            !!n.querySelector('a[href*="/notifications"]'));
          const toggles = nav ? [...nav.querySelectorAll('button[aria-expanded]')] : [];
          // Two current LinkedIn nav variants: one has data-view-name, the
          // other exposes only the single profile toggle with aria-expanded.
          const button = nav?.querySelector('button.global-nav__primary-link-me-menu-trigger') ||
            document.querySelector('button.global-nav__primary-link-me-menu-trigger') ||
            nav?.querySelector('button[data-view-name="navigation-settings"]') ||
            (toggles.length === 1 ? toggles[0] : null);
          if (!button || button.disabled) return [];
          const wasExpanded = button.getAttribute("aria-expanded") === "true";
          const menuLink = () => {
            const legacy = [...document.querySelectorAll(
              'div[role="menu"] a[data-view-name="nav-me-settings-view-profile"][href*="/in/"]')];
            if (legacy.length === 1) return legacy[0];
            // The new menu contains two links to the same profile. Only
            // aria-disabled=false identifies the actionable "View profile".
            const enabled = [...document.querySelectorAll(
              'div[role="menu"] a[aria-disabled="false"][href*="/in/"]')];
            if (enabled.length === 1) return enabled[0];
            // Observed 2026-11-05: LinkedIn's Me dropdown link has neither
            // data-view-name nor aria-disabled. Use the explicit visible label.
            const visibleProfile = [...document.querySelectorAll(
              'a[href*="/in/"]')].filter(a =>
                a.getClientRects().length > 0 &&
                /^(view profile|wyświetl profil|zobacz profil)$/i.test(
                  (a.innerText || a.textContent || "").trim()) &&
                !!a.closest('.artdeco-dropdown__content,[role="menu"],.global-nav__me-content'));
            return visibleProfile.length === 1 ? visibleProfile[0] : null;
          };
          try {
            if (!wasExpanded) button.click();
            for (let i=0; i<6 && !menuLink(); i++)
              await new Promise(resolve => setTimeout(resolve, 60));
            const anchor = menuLink();
            return anchor ? [anchor.href] : [];
          } finally {
            if (!wasExpanded && button.getAttribute("aria-expanded") === "true") {
              button.click();
              for (let i=0; i<5 && button.getAttribute("aria-expanded") === "true"; i++)
                await new Promise(resolve => setTimeout(resolve, 40));
            }
          }
        })()
        """;
}
