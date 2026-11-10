const fs=require('fs'), assert=require('assert');
const root=String.raw`C:\Users\pboru\Downloads\OpenFill__v0.1__2026-10-04_1911\OpenFill\src\OpenFill.Core\Agent\LinkedInAccountMenuProbe__v0.1__2026-11-03.cs`;
const file=fs.readFileSync(root,'utf8'),match=file.match(/public const string Javascript = """([\s\S]*?)""";/);
if(!match)throw Error('PROBE_MISSING');
const url='https://www.linkedin.com/in/pawel-boruta-osko/';
let expanded=false, clicks=0;
const button={disabled:false,getAttribute(k){return k==='aria-expanded'?String(expanded):null;},click(){expanded=!expanded;clicks++}};
const anchor={href:url,innerText:'View profile',getClientRects(){return expanded?[{left:5}]:[];},closest(s){return s.includes('.artdeco-dropdown__content')?{}:null}};
const nav={querySelector(q){if(q.startsWith('a[href*='))return {};if(q.includes('global-nav__primary-link-me-menu-trigger'))return button;return null;},querySelectorAll(q){return q==='button[aria-expanded]'?[button]:[]}};
const document={
 querySelector(q){if(q==='button.global-nav__primary-link-me-menu-trigger')return button;return null;},
 querySelectorAll(q){if(q==='nav')return [nav];if(q==='a[href*="/in/"]')return expanded?[anchor]:[];return [];}
};
const location={hostname:'www.linkedin.com'};
(async()=>{
 const found=await new Function('document','location','return ('+match[1].trim()+')')(document,location);
 assert.deepStrictEqual(found,[url]);
 assert.equal(clicks,2);
 assert.equal(expanded,false);
 console.log('D02_LINKEDIN_MENU_PROBE_4_CHECKS_OK');
})().catch(e=>{console.error(e);process.exitCode=1});
