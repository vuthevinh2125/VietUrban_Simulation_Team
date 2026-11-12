"""
Tạo các file âm thanh của VietUrban bằng tổng hợp số (không dùng file thu âm, không vướng bản quyền).
Chạy: python generate_sounds.py <thư mục ra>   (cần numpy, scipy)
Mỗi âm thanh được dựng từ nhiễu (trắng / hồng / nâu) qua bộ lọc, sóng sin / răng cưa và bao biên độ (envelope).
Các file lặp (*_loop) được trộn chéo đầu - cuối để lặp không có tiếng "cụp".
Seed cố định nên chạy lại ra đúng các file như nhau.
Trong game: 4 file *_loop giữ dạng WAV (OGG thêm khoảng lặng đầu file làm âm lặp bị hở), các tiếng ngắn đổi sang OGG
cho nhẹ repo:  ffmpeg -i x.wav -c:a libvorbis -q:a 5 x.ogg   ->   Assets/Project/Resources/Audio/
"""
import sys, os
import numpy as np
from scipy import signal
from scipy.io import wavfile

SR = 44100
rng = np.random.default_rng(1682)


def t_axis(sec):
    return np.arange(int(SR * sec)) / SR


def white(n):
    return rng.standard_normal(n)


def pink(n):
    # Lọc Paul Kellet xấp xỉ 1/f
    w = white(n)
    b = [0.049922035, -0.095993537, 0.050612699, -0.004408786]
    a = [1, -2.494956002, 2.017265875, -0.522189400]
    return signal.lfilter(b, a, w)


def brown(n):
    x = np.cumsum(white(n))
    x = signal.lfilter([1, -1], [1, -0.995], x)  # bỏ trôi DC
    return x


def bp(x, lo, hi, order=4):
    sos = signal.butter(order, [lo, hi], btype='band', fs=SR, output='sos')
    return signal.sosfilt(sos, x)


def lp(x, f, order=4):
    return signal.sosfilt(signal.butter(order, f, btype='low', fs=SR, output='sos'), x)


def hp(x, f, order=2):
    return signal.sosfilt(signal.butter(order, f, btype='high', fs=SR, output='sos'), x)


def env_ad(n, attack, decay_tau):
    t = np.arange(n) / SR
    a = np.clip(t / max(attack, 1e-4), 0, 1)
    return a * np.exp(-np.maximum(t - attack, 0) / decay_tau)


def norm(x, peak=0.9):
    m = np.max(np.abs(x))
    return x * (peak / m) if m > 0 else x


def loopify(x, fade_sec=1.0):
    """Trộn chéo phần cuối vào phần đầu để lặp liền mạch (độ dài giảm đi fade_sec)."""
    f = int(SR * fade_sec)
    if x.ndim == 1:
        x = x[:, None]
    head, tail = x[:f], x[-f:]
    w = np.linspace(0, 1, f)[:, None]
    mixed = head * np.sqrt(w) + tail * np.sqrt(1 - w)
    out = np.concatenate([mixed, x[f:-f]])
    return out[:, 0] if out.shape[1] == 1 else out


def save(folder, name, x, peak=0.9):
    x = norm(x, peak)
    pcm = np.int16(np.clip(x, -1, 1) * 32767)
    wavfile.write(os.path.join(folder, name + '.wav'), SR, pcm)
    print(f'{name}.wav  {len(x) / SR:.2f} s  {"stereo" if x.ndim > 1 else "mono"}')


# ---------------- Mưa ----------------
def rain(sec, heavy):
    n = int(SR * sec)
    out = np.zeros((n, 2))
    for ch in range(2):
        bed = hp(pink(n), 300)
        bed = lp(bed, 6000 if heavy else 4500)
        drops = np.zeros(n)
        count = int(sec * (900 if heavy else 450))
        for _ in range(count):
            i = rng.integers(0, n - 800)
            L = rng.integers(120, 600)
            d = white(L) * env_ad(L, 0.0004, rng.uniform(0.002, 0.008))
            drops[i:i + L] += d * rng.uniform(0.3, 1.0)
        drops = bp(drops, 1500, 9000, 2)
        slow = 1 + 0.12 * np.sin(2 * np.pi * rng.uniform(0.05, 0.12) * np.arange(n) / SR + rng.uniform(0, 6))
        out[:, ch] = (bed / np.std(bed) * (0.55 if heavy else 0.4) + drops / (np.std(drops) + 1e-9) * 0.35) * slow
        if heavy:
            out[:, ch] += lp(brown(n), 220) / np.std(lp(brown(n), 220)) * 0.25  # mưa xối nặng hạt
    return loopify(out, 1.0)


# ---------------- Sấm ----------------
def thunder(seed_shift):
    sec = 6.0
    n = int(SR * sec)
    t = np.arange(n) / SR
    crack = hp(white(n), 800) * env_ad(n, 0.004, 0.12) * 0.9
    rumble = lp(brown(n), 160)
    rumble = rumble / np.std(rumble)
    # Nhiều lớp rền tới chậm dần
    shape = np.zeros(n)
    for k in range(5):
        start = 0.05 + k * rng.uniform(0.25, 0.7) + seed_shift * 0.1
        rise = np.clip((t - start) / 0.18, 0, 1) ** 2  # lớp rền to dần, không bật đột ngột (tránh tiếng tách)
        shape += np.exp(-np.maximum(t - start, 0) / rng.uniform(0.6, 1.6)) * rise * rng.uniform(0.4, 1.0)
    roll = rumble * shape * 0.8 + lp(white(n), 400) * shape * 0.15
    x = crack * (0.6 if seed_shift != 1 else 0.25) + roll
    x *= np.clip((sec - t) / 1.0, 0, 1)  # tắt dần cuối
    return x


# ---------------- Phố ----------------
def motorbike_pass(sec, direction):
    n = int(SR * sec)
    t = np.arange(n) / SR
    mid = sec / 2
    dist = np.sqrt(1 + ((t - mid) * 9) ** 2)            # khoảng cách tương đối
    doppler = 1 + 0.06 * np.tanh(-(t - mid) * 2) * direction
    f0 = rng.uniform(32, 42) * doppler                   # tần số nổ máy xe tay ga
    phase = 2 * np.pi * np.cumsum(f0) / SR
    saw = signal.sawtooth(phase) + 0.5 * signal.sawtooth(2 * phase) + 0.25 * np.sin(3 * phase)
    eng = lp(saw, 900) + 0.15 * bp(white(n), 300, 2500) * (1 + np.sin(phase))
    pan = np.clip(0.5 + 0.45 * np.tanh((t - mid) * 1.5) * direction, 0, 1)
    gain = 1 / dist
    return np.stack([eng * gain * (1 - pan), eng * gain * pan], axis=1)


def horn(kind):
    # Còi xe máy "bíp bíp" (2 tiếng) hoặc còi ô tô (1 tiếng dài)
    if kind == 0:
        seg = []
        for f, L in [(420, 0.16), (0, 0.07), (420, 0.22)]:
            n = int(SR * L)
            tt = np.arange(n) / SR
            s = 0 if f == 0 else signal.square(2 * np.pi * f * tt, 0.5) * 0.6 + np.sin(2 * np.pi * f * 1.5 * tt) * 0.4
            seg.append(s * np.clip(np.minimum(tt, L - tt) / 0.01, 0, 1) if f else np.zeros(n))
        x = np.concatenate(seg)
    else:
        L = 0.55
        n = int(SR * L)
        tt = np.arange(n) / SR
        x = signal.square(2 * np.pi * 350 * tt) * 0.5 + signal.square(2 * np.pi * 440 * tt) * 0.5
        x *= np.clip(np.minimum(tt, L - tt) / 0.02, 0, 1)
    return lp(x, 3500)


def city(sec):
    n = int(SR * sec)
    hum = lp(brown(n), 300)
    hum = hum / np.std(hum) * 0.25
    out = np.stack([hum, np.roll(hum, 3000)], axis=1)
    hiss = bp(pink(n), 400, 4000)
    out += np.stack([hiss, np.roll(hiss, 7000)], axis=1) / np.std(hiss) * 0.05
    # Xe máy chạy qua
    for _ in range(int(sec / 2.2)):
        L = rng.uniform(3.0, 5.0)
        s = motorbike_pass(L, rng.choice([-1, 1])) * rng.uniform(0.15, 0.45)
        i = rng.integers(0, n - s.shape[0])
        out[i:i + s.shape[0]] += s
    # Còi xa xa
    for _ in range(int(sec / 6)):
        h = horn(int(rng.integers(0, 2))) * rng.uniform(0.04, 0.09)
        i = rng.integers(0, n - len(h))
        p = rng.uniform(0.2, 0.8)
        out[i:i + len(h), 0] += lp(h, 1800) * (1 - p)
        out[i:i + len(h), 1] += lp(h, 1800) * p
    return loopify(out, 1.5)


# ---------------- Tiếng người trong cửa hàng ----------------
def crowd(sec):
    n = int(SR * sec)
    out = np.zeros((n, 2))
    for v in range(10):  # 10 "giọng" xì xào
        src = pink(n)
        t = np.arange(n) / SR
        # Âm tiết: bao biên độ 3 - 6 Hz bật tắt như câu nói
        syll = (np.sin(2 * np.pi * rng.uniform(3, 6) * t + rng.uniform(0, 6)) > rng.uniform(-0.2, 0.4)).astype(float)
        phrase = (np.sin(2 * np.pi * rng.uniform(0.15, 0.35) * t + rng.uniform(0, 6)) > 0).astype(float)
        e = lp(syll * phrase, 25, 2)
        f1, f2 = rng.uniform(400, 800), rng.uniform(1100, 2200)
        voice = bp(src, f1 * 0.7, f1 * 1.3, 2) + 0.6 * bp(src, f2 * 0.8, f2 * 1.2, 2)
        voice = voice / np.std(voice) * e
        p = rng.uniform(0.15, 0.85)
        out[:, 0] += voice * (1 - p)
        out[:, 1] += voice * p
    out = lp(out.T, 3000).T  # xa, đục như trong phòng
    return loopify(out, 1.0)


# ---------------- Tiếng động ngắn ----------------
def footstep():
    """Giày trên sàn gạch: gót chạm (tiếng "cộc" đục + thân giày) rồi mũi chạm nhẹ sau 50 - 90 ms, thêm vang phòng ngắn."""
    L = 0.32
    n = int(SR * L)
    t = np.arange(n) / SR
    x = np.zeros(n)

    def hit(at, strength, bright):
        i = int(at * SR)
        m = n - i
        tt = np.arange(m) / SR
        click = bp(white(m), 700 * bright, 2400 * bright, 2) * np.exp(-tt / 0.003)        # mặt đế chạm gạch
        body = bp(white(m), 110, 520, 2) * np.exp(-tt / 0.011) * 1.8                         # thân giày, "cộc" trầm
        thump = np.sin(2 * np.pi * rng.uniform(52, 66) * tt) * np.exp(-tt / 0.016) * 1.3    # lực dồn xuống sàn
        x[i:] += (click * 0.28 + body + thump) * strength

    hit(0.004, 1.0, rng.uniform(0.85, 1.1))                       # gót
    hit(0.004 + rng.uniform(0.05, 0.09), rng.uniform(0.35, 0.5), rng.uniform(1.0, 1.3))  # mũi
    # Vang phòng: đuôi nhiễu tắt nhanh, nhỏ -26 dB, trộn bằng chập (convolution)
    ir_n = int(SR * 0.12)
    ir = lp(white(ir_n), 3000) * np.exp(-np.arange(ir_n) / SR / 0.035)
    ir = ir / np.max(np.abs(ir)) * 0.05
    ir[0] = 1.0
    y = lp(signal.fftconvolve(x, ir)[:n], 3200)  # bớt chói, nghe dày và trầm hơn
    return y * np.clip((L - t) / 0.05, 0, 1)


def box_drop():
    L = 0.45
    n = int(SR * L)
    t = np.arange(n) / SR
    body = np.sin(2 * np.pi * 95 * t * (1 - 0.3 * t)) * env_ad(n, 0.002, 0.07)
    card = bp(white(n), 250, 2200, 2) * env_ad(n, 0.001, 0.05) * 0.9
    rattle = bp(white(n), 2000, 5000, 2) * env_ad(n, 0.02, 0.06) * 0.25
    return body + card + rattle


def bat_swing():
    L = 0.38
    n = int(SR * L)
    t = np.arange(n) / SR
    x = white(n)
    # Bộ lọc dải quét lên rồi xuống (tiếng "vút")
    out = np.zeros(n)
    blocks = 32
    for b in range(blocks):
        s, e = b * n // blocks, (b + 1) * n // blocks
        u = b / (blocks - 1)
        fc = 400 + 2600 * np.sin(np.pi * u) ** 2
        out[s:e] = bp(x, fc * 0.7, fc * 1.3, 2)[s:e]
    return out * np.sin(np.pi * np.clip(t / L, 0, 1)) ** 2


def bat_hit():
    L = 0.4
    n = int(SR * L)
    t = np.arange(n) / SR
    crack = hp(white(n), 1500) * env_ad(n, 0.0005, 0.015)
    thump = np.sin(2 * np.pi * 110 * t * (1 - 0.4 * t)) * env_ad(n, 0.001, 0.06) * 1.2
    wood = sum(np.sin(2 * np.pi * f * t) * env_ad(n, 0.0005, d) * a for f, d, a in [(620, 0.05, 0.4), (1340, 0.03, 0.25), (2210, 0.02, 0.15)])
    oof = bp(white(n), 200, 900, 2) * env_ad(n, 0.01, 0.08) * 0.4  # tiếng "hự" đục
    return crack * 0.7 + thump + wood + oof


def sweep():
    L = 0.9
    n = int(SR * L)
    t = np.arange(n) / SR
    x = np.zeros(n)
    for start, dur in [(0.02, 0.32), (0.45, 0.36)]:
        e = np.clip((t - start) / dur, 0, 1)
        e = np.sin(np.pi * e) ** 1.5 * ((t >= start) & (t <= start + dur))
        bristle = bp(white(n), 1800, 8000, 2) * (1 + 0.6 * (np.sin(2 * np.pi * 70 * t) > 0.6))
        x += bristle * e
    return x


def wet_squish():
    L = 0.5
    n = int(SR * L)
    t = np.arange(n) / SR
    glug = np.sin(2 * np.pi * (140 + 260 * t) * t) * env_ad(n, 0.01, 0.12)
    soggy = lp(white(n), 900) * env_ad(n, 0.005, 0.1) * 0.8
    drip = sum(np.sin(2 * np.pi * f * np.maximum(t - s, 0)) * env_ad(n, 0.001, 0.02) * (t >= s) * 0.3
               for f, s in [(1800, 0.18), (2300, 0.31)])
    return glug * 0.6 + soggy + drip



# ---------------- Nhạc nền siêu thị ----------------
mtof = lambda m: 440 * 2 ** ((m - 69) / 12)

STYLES = {
    # tên file: (phong cách, BPM, 4 hợp âm (MIDI), 4 nốt gốc bass)
    'store_music_2': ('lofi', 78, [[53, 57, 60, 64], [52, 55, 59, 62], [50, 53, 57, 60], [48, 52, 55, 59]], [41, 40, 38, 36]),   # Lo-fi Afternoon: Fmaj7 Em7 Dm7 Cmaj7
    'store_music_3': ('bossa', 92, [[53, 57, 60, 64], [53, 59, 62, 64], [52, 55, 59, 62], [55, 58, 61, 64]], [38, 43, 36, 45]),  # Bossa Corner: Dm9 G13 Cmaj9 A7b9
    'store_music_4': ('jazz', 66, [[51, 55, 58, 62], [48, 51, 55, 58], [53, 56, 60, 63], [50, 53, 56, 58]], [39, 36, 41, 46]),    # Night Jazz: Ebmaj7 Cm7 Fm7 Bb7
    'store_music_5': ('pop', 96, [[48, 52, 55, 59], [57, 60, 64, 67], [53, 57, 60, 64], [55, 59, 62, 64]], [36, 33, 41, 43]),     # Sunny Aisle: Cmaj7 Am7 Fmaj7 G6
}


def store_music(style, bpm, chords, roots, bars=16):
    """Nhạc chill: piano điện (Rhodes), bass, trống theo phong cách, tiếng đĩa than; đúng 16 ô nhịp để lặp."""
    beat = 60 / bpm
    n = int(SR * bars * 4 * beat)
    out = np.zeros(n)

    def ep(freq, start, dur, vel, bright=1.0):
        i0, m = int(start * SR), int((dur + 1.2) * SR)
        m = min(m, n - i0)
        if m <= 0:
            return
        tt = np.arange(m) / SR
        tone = np.sin(2 * np.pi * freq * tt) + 0.25 * bright * np.sin(2 * np.pi * 2 * freq * tt) * np.exp(-tt / 0.3) \
            + 0.08 * bright * np.sin(2 * np.pi * 3.01 * freq * tt) * np.exp(-tt / 0.15)
        e = np.clip(tt / 0.012, 0, 1) * np.exp(-tt / 1.4) * np.clip((dur + 1.2 - tt) / 0.4, 0, 1)
        out[i0:i0 + m] += tone * e * (1 + 0.18 * np.sin(2 * np.pi * 4.5 * tt)) * vel

    def hit(at, kind, vel=1.0):
        i0 = int(at * SR)
        m = min(int(0.6 * SR), n - i0)
        if m <= 0:
            return
        tt = np.arange(m) / SR
        if kind == 'kick':
            w = np.sin(2 * np.pi * (55 + 70 * np.exp(-tt / 0.03)) * tt) * np.exp(-tt / 0.12) * 0.5
        elif kind == 'snare':
            w = bp(white(m), 900, 3500, 2) * np.exp(-tt / 0.06) * 0.16 + np.sin(2 * np.pi * 190 * tt) * np.exp(-tt / 0.05) * 0.15
        elif kind == 'clap':
            w = sum(bp(white(m), 1000, 4000, 2) * np.exp(-np.maximum(tt - d, 0) / 0.03) * (tt >= d) for d in (0, 0.01, 0.02)) * 0.07
        elif kind == 'rim':
            w = bp(white(m), 1800, 5000, 2) * np.exp(-tt / 0.008) * 0.25 + np.sin(2 * np.pi * 820 * tt) * np.exp(-tt / 0.01) * 0.1
        elif kind == 'hat':
            w = hp(white(m), 7000) * np.exp(-tt / 0.018) * 0.1
        elif kind == 'shaker':
            w = bp(white(m), 5000, 11000, 2) * np.sin(np.pi * np.clip(tt / 0.05, 0, 1)) * 0.05
        elif kind == 'ride':
            w = hp(white(m), 5000) * np.exp(-tt / 0.25) * 0.05 + np.sin(2 * np.pi * 5200 * tt) * np.exp(-tt / 0.3) * 0.015
        else:  # brush: tiếng chổi quét mặt trống
            w = bp(white(m), 1500, 7000, 2) * np.sin(np.pi * np.clip(tt / 0.18, 0, 1)) * 0.035
        out[i0:i0 + m] += w * vel

    for bar in range(bars):
        c = bar % 4
        b0 = bar * 4 * beat
        ch = chords[c]
        # Piano: lofi / pop rải trên phách 1 và 2.5; bossa đệm nghịch phách; jazz giữ dài
        comp = {'lofi': [(0, 2.2, 0.16), (2.5, 1.3, 0.09)], 'pop': [(0, 1.4, 0.14), (1.5, 0.9, 0.09), (2.5, 1.3, 0.1)],
                'bossa': [(0, 1.2, 0.13), (1.5, 0.8, 0.1), (3, 0.9, 0.11)], 'jazz': [(0, 3.6, 0.15)]}[style]
        for pos, dur, vel in comp:
            for k, note in enumerate(ch):
                ep(mtof(note), b0 + pos * beat + k * 0.016, dur * beat, vel, 0.8 if style == 'jazz' else 1.0)
        # Giai điệu: nốt trong hợp âm, cao một quãng tám
        if bar % 2 == 1 or style == 'jazz':
            mel = rng.choice([ch[1] + 12, ch[2] + 12, ch[3] + 12], 3)
            spots = [(1.0, 0.8), (3.0, 0.9)] if style != 'jazz' else [(0.5, 1.2), (2.0, 0.6), (2.67, 1.2)]
            for (pos, dur), note in zip(spots, mel):
                ep(mtof(note), b0 + pos * beat, dur * beat, 0.075)
        # Bass
        line = {'lofi': [(0, 1.5, 0), (2.5, 1.0, 7)], 'pop': [(0, 1.0, 0), (1.5, 0.5, 0), (2.5, 1.0, 7)],
                'bossa': [(0, 1.0, 0), (1.5, 0.5, 7), (2.0, 1.0, 7), (3.5, 0.5, 0)], 'jazz': [(0, 1, 0), (1, 1, 4), (2, 1, 7), (3, 1, 9)]}[style]
        for pos, dur, iv in line:
            i0 = int((b0 + pos * beat) * SR)
            m = min(int(dur * beat * SR), n - i0)
            tt = np.arange(m) / SR
            f = mtof(roots[c] - 12 + iv)
            out[i0:i0 + m] += np.sin(2 * np.pi * f * tt) * np.clip(tt / 0.01, 0, 1) * np.exp(-tt / 0.6) * 0.32
        # Trống
        for bt in range(4):
            at = b0 + bt * beat
            if style in ('lofi', 'pop'):
                hit(at, 'kick' if bt in (0, 2) else ('snare' if style == 'lofi' else 'clap'))
                if bt == 2:
                    hit(at + 0.5 * beat, 'kick', 0.8)
                for half, sw in ((0, 0.0), (1, 0.58 if style == 'lofi' else 0.5)):
                    hit(at + sw * beat, 'hat', 1.0 if half == 0 else 0.7)
            elif style == 'bossa':
                if bt in (0, 2):
                    hit(at, 'kick', 0.7)
                    hit(at + 0.75 * beat, 'kick', 0.5)
                for sx in range(4):
                    hit(at + sx * 0.25 * beat, 'shaker', 1.0 if sx == 0 else 0.6)
            else:  # jazz: ride có swing, chổi quét, kick nhẹ
                hit(at, 'ride')
                if bt in (1, 3):
                    hit(at + 0.66 * beat, 'ride', 0.7)
                hit(at, 'brush')
                if bt == 0:
                    hit(at, 'kick', 0.45)
        if style == 'bossa':  # rim click kiểu bossa nova (trải qua 2 ô nhịp)
            for pos in ([0, 1.5, 3] if bar % 2 == 0 else [1, 2.5]):
                hit(b0 + pos * beat, 'rim')
    crackle = lp(pink(n), 6000) * 0.012
    for _ in range(int(n / SR * 8)):
        i = rng.integers(0, n - 50)
        crackle[i:i + 30] += white(30) * np.exp(-np.arange(30) / 6) * rng.uniform(0.02, 0.08)
    out = lp(out, 5500 if style != 'pop' else 7000) + crackle * (0.6 if style == 'pop' else 1.0)
    return np.stack([out, np.roll(out, int(0.012 * SR))], axis=1)


def main(folder):
    os.makedirs(folder, exist_ok=True)
    save(folder, 'rain_loop', rain(12, False), 0.7)
    save(folder, 'storm_loop', rain(12, True), 0.8)
    for i in range(3):
        save(folder, f'thunder_{i + 1}', thunder(i), 0.95)
    save(folder, 'city_loop', city(24), 0.7)
    save(folder, 'crowd_loop', crowd(12), 0.6)
    for i in range(4):
        save(folder, f'footstep_{i + 1}', footstep(), 0.75)
    save(folder, 'horn_1', horn(0), 0.8)
    save(folder, 'horn_2', horn(1), 0.8)
    save(folder, 'box_drop', box_drop(), 0.9)
    save(folder, 'bat_swing', bat_swing(), 0.8)
    save(folder, 'bat_hit', bat_hit(), 0.95)
    save(folder, 'sweep', sweep(), 0.6)
    save(folder, 'wet_squish', wet_squish(), 0.8)
    for name, (style, bpm, chords, roots) in STYLES.items():
        save(folder, name, store_music(style, bpm, chords, roots), 0.7)


if __name__ == '__main__':
    main(sys.argv[1] if len(sys.argv) > 1 else 'out')
