import hashlib,json,re,sys
from html import unescape
from pathlib import Path
output=Path(sys.argv[1]);base=sys.argv[2]
def html(route): return (output/route/'index.html').read_text(encoding='utf-8-sig')
def links(text,kind):
    section=re.search(r'<section class="'+kind+r'"[^>]*>(.*?)</section>',text,re.S)
    return [] if not section else [(unescape(url),unescape(re.sub('<[^>]*>','',title))) for url,title in re.findall(r'<a href="([^"]+)">(.*?)</a>',section[1],re.S)]
def counts(text):
    comments=re.findall(r'<!--\s*(srbiz-related:.*?)-->',text,re.S)
    assert len(comments)==1, 'one relationship diagnostic comment required'
    match=re.fullmatch(r'srbiz-related:v1 missing=(\d+) ambiguous=(\d+) conflict=(\d+) invalid=(\d+)\s*',comments[0]);assert match,'diagnostic must contain fixed counters only'
    return tuple(map(int,match.groups()))
post_schemas=[json.loads(unescape(payload)) for payload in re.findall(r'<script[^>]+type="application/ld\+json"[^>]*>(.*?)</script>',html('insights/valid-v2'),re.S)]
posting=next(item for item in post_schemas if item.get('@type')=='BlogPosting' and isinstance(item.get('author'),dict))
assert posting['author']['url']=='https://trust-fixture.invalid'+base+'/authors/silushangxun-editorial-team/',posting['author']['url']
main=html('insights/relation-main');forward=links(main,'related-entities')
assert [url for url,_ in forward]==[base+'/companies/relation-'+x+'/' for x in 'abcdefg']+[base+'/companies/task4-trust-company/'],forward
assert [title for _,title in forward[:7]]==['当前企业 '+x for x in 'abcdefg'],forward
assert counts(main)[0]>0 and counts(main)[1]==0 and counts(main)[2]>0 and counts(main)[3]>0,counts(main)
assert '过期标签' not in main and '禁止泄露目标' not in main,'hidden target metadata leaked'
fallback=links(html('insights/relation-explicit'),'related-entities')
expected_fallback=['a','c'] if not base else ['b','c','d']
assert [url for url,_ in fallback]==[base+'/companies/relation-'+x+'/' for x in expected_fallback],fallback
# Restrict checks to template-owned navigation containers; source article links are not rewritten.
from html.parser import HTMLParser
class Navigation(HTMLParser):
    def __init__(self):super().__init__();self.stack=[];self.urls=[]
    def handle_starttag(self,tag,attrs):
        values=dict(attrs);classes=set(values.get('class','').split())
        owned=tag=='nav' or bool(classes & {'breadcrumb','hero-actions','section-actions','tag-index-card','company-card','article-card','related-content','related-entities'})
        if tag=='a' and any(self.stack) and 'href' in values:self.urls.append(values['href'])
        if tag not in {'img','input','br','hr','meta','link','source','wbr','area','base','embed','param','track','col'}:self.stack.append(owned)
    def handle_endtag(self,tag):
        if self.stack:self.stack.pop()
if base:
    navigation_count=0
    # Static 404.html is copied source, not a rendered template; its bytes remain in the parity snapshot.
    for path in output.rglob('index.html'):
        parser=Navigation();parser.feed(path.read_text(encoding='utf-8-sig'))
        for url in parser.urls:
            if url.startswith('/') and not url.startswith('//'):
                assert url.startswith(base+'/'),(str(path.relative_to(output)),url)
                navigation_count+=1
    assert navigation_count>20,'nonroot fixture must exercise generated navigation across pages'
company=html('companies/task4-trust-company');reverse=links(company,'related-content')
expected=['relation-explicit','valid-v2','relation-main','relation-reverse-0','relation-reverse-1','relation-reverse-2']
assert [url for url,_ in reverse]==[base+'/insights/'+slug+'/' for slug in expected],reverse
assert counts(company)==(0,0,0,0),'reverse scan must not attribute other sources diagnostics to company'
assert reverse[0][1]=='显式优先报道','use current target title'
assert not links(html('insights/relation-same-title'),'related-entities'),'title-only identity forbidden'
assert '<section class="related-entities"' not in html('insights/relation-same-title'),'empty relation section forbidden'
empty=html('companies/relation-date-empty')
assert '核验日期</dt>' not in empty,'empty VerifiedAt must hide and must not fall back to updated'
zone=html('companies/relation-date-zone')
assert re.search(r'核验日期</dt><dd>2026-07-25',zone),'verified date must preserve date meaning'
# All public bytes are compared. Only exact internal build state/marker and .bukit reports are omitted.
if len(sys.argv)>3:
    current={str(path.relative_to(output)):hashlib.sha256(path.read_bytes()).hexdigest() for path in sorted(output.rglob('*')) if path.is_file() and '.bukit' not in path.relative_to(output).parts and path.name not in {'.bukit-output-marker','.bukit-build-state.json'}}
    snapshot=Path(sys.argv[4])
    if sys.argv[3]=='snapshot':snapshot.write_text(json.dumps(current,sort_keys=True))
    else:
        expected=json.loads(snapshot.read_text());assert current==expected,{'added':sorted(current.keys()-expected.keys()),'removed':sorted(expected.keys()-current.keys()),'changed':[p for p in current.keys()&expected.keys() if current[p]!=expected[p]]}
print('PASS: relationship navigation, diagnostics, date, public-byte contract')
