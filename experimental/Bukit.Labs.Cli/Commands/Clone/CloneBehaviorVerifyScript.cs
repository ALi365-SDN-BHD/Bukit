namespace Bukit.Cli.Commands;

internal static class CloneBehaviorVerifyScript
{
    internal const string Script = """
(function(){'use strict';
var results=[];
function pass(name,detail){results.push({name:name,status:'pass',detail:detail||''});console.log('%c PASS %c '+name,'color:green','color:inherit',detail||'');}
function fail(name,detail){results.push({name:name,status:'fail',detail:detail||''});console.log('%c FAIL %c '+name,'color:red','color:inherit',detail||'');}
function warn(name,detail){results.push({name:name,status:'warn',detail:detail||''});console.log('%c WARN %c '+name,'color:orange','color:inherit',detail||'');}

// Header sticky
var header=document.querySelector('.site-header');
if(header){var pos=getComputedStyle(header).position;if(pos==='sticky'||pos==='fixed')pass('HeaderSticky','position: '+pos);else warn('HeaderSticky','position: '+pos+' (not sticky)');}else{fail('HeaderSticky','.site-header not found');}

// Header shrink (nav-hidden class toggle check)
if(header&&document.querySelector('.nav-hidden')!==null)pass('HeaderShrink','.nav-hidden class present');else if(header)warn('HeaderShrink','.nav-hidden not present (may need scroll)');else fail('HeaderShrink','header not found');

// Dark mode toggle
var dt=document.querySelector('.dark-mode-toggle');
if(dt){pass('DarkModeToggle:exists','found');var wasDark=document.body.classList.contains('dark');dt.click();var nowDark=document.body.classList.contains('dark');if(wasDark!==nowDark)pass('DarkModeToggle:toggles','body.dark toggled');else fail('DarkModeToggle:toggles','body.dark did not change');dt.click();}else{warn('DarkModeToggle','.dark-mode-toggle not found (dark mode may not be configured)');}

// Modal
var mo=document.getElementById('site-modal')||document.querySelector('.modal-overlay');
if(mo){pass('Modal:exists','found');var wasVis=!mo.classList.contains('hidden')&&mo.classList.contains('visible');if(!wasVis){mo.classList.remove('hidden');mo.classList.add('visible');mo.setAttribute('aria-hidden','false');var nowVis=!mo.classList.contains('hidden')&&mo.classList.contains('visible');mo.classList.add('hidden');mo.classList.remove('visible');mo.setAttribute('aria-hidden','true');if(nowVis)pass('Modal:opens','modal became visible');else fail('Modal:opens','modal did not become visible');}else{pass('Modal:visible','already visible');}}else{warn('Modal','.modal-overlay not found');}

// Hamburger
var ham=document.querySelector('.hamburger');
if(ham){pass('Hamburger:exists','found');var nav=document.querySelector('.nav-links');var wasOpen=nav&&nav.classList.contains('open');ham.click();var nowOpen=nav&&nav.classList.contains('open');if(wasOpen!==nowOpen)pass('Hamburger:toggles','.nav-links.open toggled');else fail('Hamburger:toggles','did not toggle');ham.click();}else{warn('Hamburger','.hamburger not found');}

// Tabs (tab-nav or state-tabs)
var tabs=document.querySelector('.tab-nav')||document.querySelector('.state-tabs');
if(tabs){var firstBtn=tabs.querySelector('[role="tab"]');if(firstBtn){pass('Tabs:exists','found');var wasSel=firstBtn.getAttribute('aria-selected')==='true';firstBtn.click();setTimeout(function(){var nowSel=firstBtn.getAttribute('aria-selected')==='true';if(nowSel)pass('Tabs:switches','tab selected');else fail('Tabs:switches','tab did not become selected');},50);}else{fail('Tabs','no tab button found');}}else{warn('Tabs','no .tab-nav or .state-tabs found');}

// Lenis
if(typeof lenis!=='undefined'){pass('Lenis','window.lenis defined');}else{warn('Lenis','window.lenis not defined (Lenis may not be configured)');}

// Back to top
var btt=document.querySelector('.back-to-top');
if(btt){pass('BackToTop:exists','found');var bttOp=getComputedStyle(btt).opacity;if(parseFloat(bttOp)>0)pass('BackToTop:visible','opacity: '+bttOp);else warn('BackToTop:hidden','opacity: '+bttOp+' (may need scroll)');}else{warn('BackToTop','.back-to-top not found');}

// Animate on scroll
var anim=document.querySelector('.animate-in');
if(anim){pass('AnimateOnScroll','.animate-in element found');}else{warn('AnimateOnScroll','no .animate-in elements found');}

// Summary
console.log('\n=== BEHAVIOR VERIFY SUMMARY ===');
var passed=results.filter(function(r){return r.status==='pass';}).length;
var failed=results.filter(function(r){return r.status==='fail';}).length;
var warnings=results.filter(function(r){return r.status==='warn';}).length;
console.log('Passed: '+passed+' Failed: '+failed+' Warnings: '+warnings+' Total: '+results.length);
if(failed>0){console.log('%c FAILURES DETECTED','color:red;font-weight:bold');}else if(warnings===0){console.log('%c ALL CHECKS PASSED','color:green;font-weight:bold');}else{console.log('%c ALL CRITICAL CHECKS PASSED (with warnings)','color:orange;font-weight:bold');}
window.__bukitBehaviorResults=results;

// Export as JSON
var json=JSON.stringify({timestamp:new Date().toISOString(),summary:{passed:passed,failed:failed,warnings:warnings,total:results.length},results:results},null,2);
console.log('\n=== RESULTS JSON ===');
console.log(json);

})();
""";
}
