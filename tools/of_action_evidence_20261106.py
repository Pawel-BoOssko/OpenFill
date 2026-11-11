import re
NUM={"jeden":1,"jedną":1,"jedno":1,"one":1,"dwa":2,"dwie":2,"two":2,
     "trzy":3,"three":3,"cztery":4,"four":4,"pięć":5,"five":5}
def literal_number(txt):
    if txt.isdigit():return int(txt)
    return NUM.get(txt.lower())
def reported_units(kind,requested,summary,status):
    if not summary:return 0,"no_summary"
    s=summary.lower()
    if kind=="invitation":
        lines=re.findall(r"(?m)^\s*(\d{1,3})[.)]\s*([^\n]{0,450})",summary)
        sent=set()
        for idx,line in lines:
            if re.search(r"nie wysłano|nie wysyłano|not sent|invitation not sent",line,re.I):continue
            if re.search(r"wysłano|invitation sent|sent successfully|zaproszenie wysłane",line,re.I):
                sent.add(int(idx))
        if sent and (requested is None or len(sent)<=requested):
            return len(sent),"agent_listed_ui_confirmation"
        m=re.search(r"(\d+)\s+(?:zaprosze|invitations?).{0,35}(?:wysłan|sent)|(?:wysłano|sent)\s+(\d+)\s+(?:zaprosze|invitations?)",s)
        if m:return int(next(x for x in m.groups() if x)),"agent_explicit_count"
        return 0,"not_counted"
    if kind=="registration":
        if re.search(r"rejestracja powiodła się|profil (?:jest już )?aktywny|konto aktywne|account is active|registration successful|registration completed",s):
            return 1,"agent_reports_active_registration"
        return 0,"registration_not_confirmed_active"
    if kind=="message":
        if (requested==1 and status=="success" and
            re.search(r"wysłano.{0,45}wiadomość|wiadomość.{0,40}wysłan|sent.{0,50}message|wysłano:|message sent|delivered|w rozmowie jako",s)):
            return 1,"agent_reports_sent_message"
        return 0,"message_not_confirmed"
    if kind!="publication":return 0,"not_an_effect_category"
    lines=re.findall(r"(?m)^\s*(\d{1,3})[.)]\s*([^\n]{0,500})",summary)
    numbered=set()
    for idx,line in lines:
        if re.search(r"opublikowan|published|posted|wpis dodany",line,re.I):
            numbered.add(int(idx))
    if numbered and (requested is None or len(numbered)<=requested):
        return len(numbered),"agent_listed_posts"
    patterns=[
        r"(?:opublikowano|opublikowałem|published|posted)\s+(?:all\s+|wszystkie\s+|łącznie\s+|pierwsze\s+|first\s+|up to\s+)?(\d+|three|trzy|five|pięć|two|dwa|dwie|four|cztery)\s+(?:.{0,34}?)?(?:post|wpis|reel|film|answer|odpowied|komentarz)",
        r"(?:published all|opublikowano i zweryfikowano|opublikowano)\s+(three|trzy|five|pięć|two|dwa|dwie|four|cztery)\s+(?:.{0,45}?)?(?:post|wpis|reel|answer|odpowied)",
        r"(\d+|three|trzy|five|pięć|two|dwa|dwie|four|cztery)\s+(?:post|wpis|reel|answer|odpowied).{0,55}(?:opublikowan|published)"
    ]
    for pat in patterns:
        m=re.search(pat,s)
        if m:
            n=literal_number(m.group(1))
            if n and (requested is None or n<=requested):
                return n,"agent_reports_counted_publication"
    if re.search(r"reel opublikowany|opublikowano reel|reel published|published reel|recenzję wysłano|review published|opublikowan.*post|published one post",s):
        return 1,"agent_reports_single_publication"
    urls=re.findall(r"https?://[^\s<>\"')]+",summary)
    published_urls={u.rstrip(".,;") for u in urls if re.search(r"(instagram.com/.*/(?:reel|p)/|linkedin.com/feed/update/|facebook.com/.*/posts/)",u,re.I)}
    if published_urls and re.search(r"opublikowan|published|posted|опубликован|url publikacji",s):
        return len(published_urls),"agent_reports_publication_urls"
    return 0,"publication_not_confirmed"
