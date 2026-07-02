using System.Text;

namespace Bukit.Labs.Cli.Commands;

internal static class CloneBehaviorGenerator
{
    internal static int CountBehaviors(CloneBehaviors? b)
    {
        if (b is null) return 0;
        var count = 0;
        if (b.StickyHeader) count++;
        if (b.CardHoverLift) count++;
        if (b.AnimateOnScroll) count++;
        if (b.ScrollShrinkNav) count++;
        if (b.DarkModeToggle) count++;
        if (b.MobileHamburger) count++;
        if (b.SmoothScroll) count++;
        if (b.BackToTop) count++;
        if (b.HasModal) count++;
        if (b.HasDropdown) count++;
        if (b.HasTabs) count++;
        if (b.UseLenis) count++;
        return count;
    }

    internal static string GenerateBehaviorCss(CloneBehaviors b, CloneTokens t)
    {
        var sb = new StringBuilder();

        if (b.StickyHeader)
        {
            sb.AppendLine("""
.site-header { position: sticky; top: 0; z-index: 100; }

""");
        }

        if (b.ScrollShrinkNav)
        {
            sb.AppendLine("""
.site-header { transition: transform 0.3s ease; }
.nav-hidden { transform: translateY(-100%); }

""");
        }

        if (b.CardHoverLift)
        {
            var lift = CloneStyleSheetGenerator.C(t.HoverLift, "3px");
            var shadow = CloneStyleSheetGenerator.C(t.HoverShadow, "var(--modal-shadow)");
            sb.AppendLine($$"""
.card { transition: transform 0.2s ease, box-shadow 0.2s ease; }
.card:hover { transform: translateY(-{{lift}}); box-shadow: {{shadow}}; }

""");
        }

        if (b.AnimateOnScroll)
        {
            var style = b.AnimationStyle ?? "fadeInUp";
            var animName = style switch
            {
                "slideUp" => "slideUp",
                "scaleIn" => "scaleIn",
                "fadeIn" => "fadeIn",
                _ => "fadeInUp"
            };
            var translateInit = style switch
            {
                "scaleIn" => "scale(0.92)",
                "fadeIn" => "translateY(0)",
                _ => "translateY(20px)"
            };

            switch (style)
            {
                case "slideUp":
                    sb.AppendLine("""
@keyframes slideUp {
  from { opacity: 0; transform: translateY(40px); }
  to   { opacity: 1; transform: translateY(0); }
}

""");
                    break;
                case "scaleIn":
                    sb.AppendLine("""
@keyframes scaleIn {
  from { opacity: 0; transform: scale(0.92); }
  to   { opacity: 1; transform: scale(1); }
}

""");
                    break;
                case "fadeIn":
                    sb.AppendLine("""
@keyframes fadeIn {
  from { opacity: 0; }
  to   { opacity: 1; }
}

""");
                    break;
                default:
                    sb.AppendLine("""
@keyframes fadeInUp {
  from { opacity: 0; transform: translateY(20px); }
  to   { opacity: 1; transform: translateY(0); }
}

""");
                    break;
            }
            sb.AppendLine($$"""
.animate-in { opacity: 0; transform: {{translateInit}}; }
.animate-visible { animation: {{animName}} 0.55s ease forwards; }

""");
        }

        if (b.MobileHamburger)
        {
            sb.AppendLine("""
.hamburger { display: none; flex-direction: column; gap: 5px; padding: 8px; border: none; background: none; cursor: pointer; }
.hamburger-bar { display: block; width: 22px; height: 2.5px; border-radius: 2px; background: var(--text); transition: transform 0.25s ease, opacity 0.25s ease; }

@media (max-width: var(--bp-mobile)) {
  .hamburger { display: flex; }
  .nav-links { display: none; flex-direction: column; width: 100%; gap: 8px; padding-top: 12px; }
  .nav-links.open { display: flex; }
}

""");
        }

        if (b.DarkModeToggle)
        {
            sb.AppendLine("""
.dark-mode-toggle { display: inline-flex; align-items: center; gap: 6px; padding: 6px 10px; border: 1px solid var(--border); border-radius: var(--radius); background: var(--surface); color: var(--text); font: inherit; font-size: 0.88rem; cursor: pointer; }
.dark-mode-toggle:hover { background: var(--surface-muted); }

body.dark { --bg: #1a1a2e; --surface: #16213e; --surface-muted: #0f3460; --text: #eaeaea; --muted: #a0a0b0; --border: #2a2a4a; }
body.dark img { opacity: 0.9; }
body.dark .site-header { background: rgba(22, 33, 62, 0.92); }

""");
        }

        if (b.HasModal)
        {
            sb.AppendLine("""
.modal-overlay { position: fixed; inset: 0; z-index: 200; display: flex; align-items: center; justify-content: center; background: rgba(0,0,0,0.45); opacity: 0; visibility: hidden; transition: opacity 0.25s ease, visibility 0.25s ease; }
.modal-overlay.visible { opacity: 1; visibility: visible; }
.modal-container { max-width: 560px; width: 90%; max-height: 80vh; overflow-y: auto; padding: 28px 32px; border-radius: var(--radius); background: var(--surface); box-shadow: var(--modal-shadow); }
.modal-header { display: flex; align-items: center; justify-content: space-between; margin-bottom: 20px; }
.modal-title { font-family: var(--heading-font, inherit); font-size: 1.3rem; font-weight: 700; margin: 0; }
.modal-close { padding: 6px 10px; border: none; background: none; font-size: 1.4rem; cursor: pointer; color: var(--muted); line-height: 1; }
.modal-close:hover { color: var(--text); }
.modal-body p { margin: 0.6em 0; color: var(--muted); }

""");
        }

        if (b.HasDropdown)
        {
            sb.AppendLine("""
.dropdown { position: relative; display: inline-block; }
.dropdown-trigger { display: inline-flex; align-items: center; gap: 6px; padding: 8px 14px; border: 1px solid var(--border); border-radius: var(--radius); background: var(--surface); color: var(--text); font: inherit; cursor: pointer; }
.dropdown-trigger:hover { background: var(--surface-muted); }
.dropdown-caret { font-size: 0.75rem; transition: transform 0.2s ease; }
.dropdown.open .dropdown-caret { transform: rotate(180deg); }
.dropdown-menu { position: absolute; top: calc(100% + 6px); left: 0; min-width: 180px; padding: 6px 0; border: 1px solid var(--border); border-radius: var(--radius); background: var(--surface); box-shadow: var(--dropdown-shadow); z-index: 150; }
.dropdown-item { display: block; padding: 8px 14px; color: var(--text); font-size: 0.92rem; }
.dropdown-item:hover { background: var(--surface-muted); color: var(--primary); }

""");
        }

        if (b.HasTabs)
        {
            sb.AppendLine("""
.tabs { margin: 20px 0; }
.tab-nav { display: flex; gap: 2px; border-bottom: 2px solid var(--border); margin-bottom: 18px; overflow-x: auto; }
.tab-btn { padding: 10px 18px; border: none; border-bottom: 2px solid transparent; margin-bottom: -2px; background: none; color: var(--muted); font: inherit; font-weight: 600; cursor: pointer; white-space: nowrap; transition: color 0.15s ease, border-color 0.15s ease; }
.tab-btn:hover { color: var(--text); }
.tab-btn[aria-selected="true"] { color: var(--primary); border-bottom-color: var(--primary); }
.tab-panel { padding: 4px 0; }
.tab-panel:not(.hidden) { display: block; }

""");
        }

        return sb.ToString().TrimEnd('\r', '\n');
    }

    internal static string GenerateBehaviorsJs(CloneBehaviors b)
    {
        var sb = new StringBuilder();
        sb.AppendLine("(function(){'use strict';");
        sb.AppendLine();

        if (b.ScrollShrinkNav)
        {
            var threshold = b.ScrollThreshold > 0 ? b.ScrollThreshold : 60;
            var reveal = Math.Max(10, threshold / 6);
            sb.AppendLine($"var h=document.querySelector('.site-header');\nvar s=0;\nwindow.addEventListener('scroll',function(){{var n=window.scrollY;if(n>{threshold}&&n>s)h.classList.add('nav-hidden');else if(n<{reveal}||n<s)h.classList.remove('nav-hidden');s=n}},{{passive:true}});\n");
        }

        if (b.MobileHamburger)
        {
            sb.AppendLine("""
var btn=document.querySelector('.hamburger');
var nav=document.querySelector('.nav-links');
if(btn&&nav){btn.addEventListener('click',function(){var o=nav.classList.toggle('open');btn.setAttribute('aria-expanded',String(o));});}

""");
        }

        if (b.DarkModeToggle)
        {
            sb.AppendLine("""
var t=document.createElement('button');
t.className='dark-mode-toggle';
t.textContent='☀️';
t.title='Toggle dark mode';
var hh=document.querySelector('.site-header');
if(hh)hh.appendChild(t);
var stored=localStorage.getItem('theme');
if(stored==='dark')document.body.classList.add('dark');
t.addEventListener('click',function(){var d=document.body.classList.toggle('dark');localStorage.setItem('theme',d?'dark':'light');t.textContent=d?'🌙':'☀️';});

""");
        }

        if (b.SmoothScroll)
        {
            sb.AppendLine("""
document.querySelectorAll('a[href^=\"#\"]').forEach(function(a){a.addEventListener('click',function(e){var id=this.getAttribute('href').slice(1);var el=document.getElementById(id);if(el){e.preventDefault();el.scrollIntoView({behavior:'smooth',block:'start'});}});});

""");
        }

        if (b.BackToTop)
        {
            sb.AppendLine("""
var btt=document.createElement('button');
btt.textContent='↑';
btt.className='back-to-top';
btt.setAttribute('aria-label','Back to top');
btt.style.cssText='position:fixed;bottom:24px;right:24px;width:44px;height:44px;border-radius:50%;border:1px solid var(--border);background:var(--surface);color:var(--text);font-size:1.2rem;cursor:pointer;opacity:0;transition:opacity 0.3s;z-index:90;';
document.body.appendChild(btt);
window.addEventListener('scroll',function(){btt.style.opacity=window.scrollY>400?'1':'0';},{passive:true});
btt.addEventListener('click',function(){window.scrollTo({top:0,behavior:'smooth'});});

""");
        }

        if (b.AnimateOnScroll)
        {
            sb.AppendLine("""
var observer=new IntersectionObserver(function(entries){entries.forEach(function(e){if(e.isIntersecting)e.target.classList.add('animate-visible');});},{threshold:0.15});
document.querySelectorAll('.animate-in').forEach(function(el){observer.observe(el);});

""");
        }

        if (b.HasModal)
        {
            sb.AppendLine("""
var mo=document.getElementById('site-modal');
if(mo){var mc=mo.querySelector('.modal-close');if(mc)mc.addEventListener('click',function(){mo.classList.add('hidden');mo.classList.remove('visible');mo.setAttribute('aria-hidden','true');});mo.addEventListener('click',function(e){if(e.target===mo){mo.classList.add('hidden');mo.classList.remove('visible');mo.setAttribute('aria-hidden','true');}});document.addEventListener('keydown',function(e){if(e.key==='Escape'&&mo.classList.contains('visible')){mo.classList.add('hidden');mo.classList.remove('visible');mo.setAttribute('aria-hidden','true');}});var triggers=document.querySelectorAll('[data-modal-trigger]');triggers.forEach(function(btn){btn.addEventListener('click',function(){mo.classList.remove('hidden');mo.classList.add('visible');mo.setAttribute('aria-hidden','false');});});}

""");
        }

        if (b.HasDropdown)
        {
            sb.AppendLine("""
document.querySelectorAll('.dropdown-trigger').forEach(function(btn){btn.addEventListener('click',function(e){e.stopPropagation();var dd=btn.closest('.dropdown');var menu=dd.querySelector('.dropdown-menu');var open=dd.classList.toggle('open');btn.setAttribute('aria-expanded',String(open));if(menu)menu.hidden=!open;});});
document.addEventListener('click',function(e){document.querySelectorAll('.dropdown.open').forEach(function(dd){if(!dd.contains(e.target)){dd.classList.remove('open');dd.querySelector('.dropdown-trigger').setAttribute('aria-expanded','false');var menu=dd.querySelector('.dropdown-menu');if(menu)menu.hidden=true;}});});

""");
        }

        if (b.HasTabs)
        {
            sb.AppendLine("""
document.querySelectorAll('.tab-nav').forEach(function(nav){var btns=nav.querySelectorAll('.tab-btn');btns.forEach(function(btn){btn.addEventListener('click',function(){var panelId=btn.getAttribute('aria-controls');btns.forEach(function(b){b.setAttribute('aria-selected','false');});btn.setAttribute('aria-selected','true');var parent=nav.closest('.tabs');if(parent){parent.querySelectorAll('.tab-panel').forEach(function(p){p.classList.add('hidden');});var panel=document.getElementById(panelId);if(panel)panel.classList.remove('hidden');}});});if(btns.length>0){btns[0].click();}});

""");
        }

        if (b.UseLenis)
        {
            sb.AppendLine("""
var lenis=new Lenis({duration:1.2,easing:function(t){return Math.min(1,1.001-Math.pow(2,-10*t))},smoothWheel:true});
function raf(time){lenis.raf(time);requestAnimationFrame(raf);}
requestAnimationFrame(raf);

""");
        }

        sb.AppendLine("})();");
        return sb.ToString();
    }
}
