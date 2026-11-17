"""Monta as cenas 1920x1080 do vídeo de demonstração a partir dos quadros gravados.

Uso: python scripts/montar-video.py <pasta do vídeo>
A pasta deve ter bruto/*.png, laudo.png e roteiro.txt (gerados por: dotnet run --project tests/APACDigital.Tests -- video).
Gera <pasta>/cenas/*.png e <pasta>/cenas/roteiro.txt (arquivo|ms) para o modo "codificar".
"""
import os
import sys
from PIL import Image, ImageDraw, ImageFilter, ImageFont

L, A = 1920, 1080
FONTES = os.path.join(os.environ.get("WINDIR", r"C:\Windows"), "Fonts")


def fonte(nome, tam):
    return ImageFont.truetype(os.path.join(FONTES, nome), tam)


F_TITULO = fonte("segoeuib.ttf", 64)
F_SUB = fonte("seguisb.ttf", 20)
F_PASSO = fonte("seguisb.ttf", 30)
F_PASSO_ATUAL = fonte("segoeuib.ttf", 31)
F_NUM = fonte("segoeuib.ttf", 24)
F_LEGENDA = fonte("seguisb.ttf", 32)
F_ICONE = fonte("SegoeIcons.ttf", 46)

AZUL = (10, 99, 214)
TEXTO = (14, 44, 75)
MUTADO = (110, 136, 164)
PASSOS = ["Médico(a) solicitante", "Dados do paciente", "Procedimento e CID-10", "Justificativa", "Gerar o PDF"]


def fundo():
    img = Image.new("RGB", (L, A))
    px = img.load()
    topo, base = (238, 247, 255), (191, 224, 247)
    for y in range(A):
        for x in range(0, L, 4):
            t = min(1.0, (x / L) * 0.35 + (y / A) * 0.75)
            c = tuple(int(topo[i] + (base[i] - topo[i]) * t) for i in range(3))
            for dx in range(4):
                px[x + dx, y] = c
    camada = Image.new("RGBA", (L, A), (0, 0, 0, 0))
    d = ImageDraw.Draw(camada)
    d.ellipse((1150, -500, 2500, 500), fill=(120, 210, 255, 120))
    d.ellipse((-700, 650, 700, 1500), fill=(80, 190, 250, 100))
    camada = camada.filter(ImageFilter.GaussianBlur(160))
    img = Image.alpha_composite(img.convert("RGBA"), camada)
    ondas = Image.new("RGBA", (L, A), (0, 0, 0, 0))
    d = ImageDraw.Draw(ondas)
    for desloc, alfa in ((0, 45), (60, 28)):
        pts = []
        for i in range(0, L + 1, 8):
            t = i / L
            y = 760 + desloc + 70 * (1 - t) * t * 4 * (0.5 - t) * 2 - 40 * t
            pts.append((i, y))
        d.line(pts, fill=(26, 138, 240, alfa), width=3)
    return Image.alpha_composite(img, ondas)


def vidro(base, caixa, raio=28, alfa=165):
    x0, y0, x1, y1 = caixa
    sombra = Image.new("RGBA", base.size, (0, 0, 0, 0))
    ImageDraw.Draw(sombra).rounded_rectangle((x0, y0 + 10, x1, y1 + 10), raio, fill=(40, 110, 180, 60))
    base.alpha_composite(sombra.filter(ImageFilter.GaussianBlur(22)))
    painel = Image.new("RGBA", base.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(painel)
    d.rounded_rectangle(caixa, raio, fill=(255, 255, 255, alfa), outline=(255, 255, 255, 235), width=2)
    base.alpha_composite(painel)


def com_sombra(base, img, pos, raio=14, desfoque=30, alfa=95):
    x, y = pos
    w, h = img.size
    sombra = Image.new("RGBA", base.size, (0, 0, 0, 0))
    ImageDraw.Draw(sombra).rounded_rectangle((x + 6, y + 22, x + w - 6, y + h + 16), raio, fill=(15, 55, 105, alfa))
    base.alpha_composite(sombra.filter(ImageFilter.GaussianBlur(desfoque)))
    mascara = Image.new("L", img.size, 0)
    ImageDraw.Draw(mascara).rounded_rectangle((0, 0, w - 1, h - 1), raio, fill=255)
    base.paste(img, pos, mascara)


def quebrar(texto, f, largura):
    palavras, linhas, atual = texto.split(), [], ""
    for p in palavras:
        teste = (atual + " " + p).strip()
        if f.getlength(teste) <= largura:
            atual = teste
        else:
            linhas.append(atual)
            atual = p
    if atual:
        linhas.append(atual)
    return linhas


def marca(img, x, y):
    tile = Image.new("RGBA", (96, 96), (0, 0, 0, 0))
    grad = Image.new("RGBA", (96, 96))
    gp = grad.load()
    for i in range(96):
        for j in range(96):
            t = (i + j) / 190
            gp[i, j] = (int(31 + (10 - 31) * t), int(182 + (99 - 182) * t), int(240 + (214 - 240) * t), 255)
    m = Image.new("L", (96, 96), 0)
    ImageDraw.Draw(m).rounded_rectangle((0, 0, 95, 95), 26, fill=255)
    tile.paste(grad, (0, 0), m)
    ImageDraw.Draw(tile).text((48, 50), "\uE8A5", font=F_ICONE, fill="white", anchor="mm")
    com_sombra(img, tile.convert("RGB"), (x, y), raio=26, desfoque=16, alfa=80)
    d = ImageDraw.Draw(img)
    d.text((x + 120, y + 4), "APAC Digital", font=F_TITULO, fill=TEXTO)
    d.text((x + 124, y + 92), "LAUDO DE APAC DO SUS EM PDF", font=F_SUB, fill=(63, 100, 136))


def janela(img, quadro, x=150):
    w = int(quadro.width * 1.18)
    h = int(quadro.height * 1.18)
    q = quadro.resize((w, h), Image.LANCZOS)
    com_sombra(img, q, (x, (A - h) // 2), raio=12)


def lado_direito(img, passo, legenda):
    x0 = 930
    marca(img, x0, 120)
    d = ImageDraw.Draw(img)
    y = 330
    for i, nome in enumerate(PASSOS, start=1):
        cx, cy = x0 + 26, y + 26
        if i < passo:
            d.ellipse((cx - 24, cy - 24, cx + 24, cy + 24), fill=(214, 236, 252))
            d.text((cx, cy + 1), "\uE73E", font=fonte("SegoeIcons.ttf", 22), fill=AZUL, anchor="mm")
            d.text((x0 + 74, cy), nome, font=F_PASSO, fill=MUTADO, anchor="lm")
        elif i == passo:
            vidro(img, (x0 - 14, y - 10, x0 + 760, y + 62), raio=20, alfa=200)
            d = ImageDraw.Draw(img)
            d.ellipse((cx - 24, cy - 24, cx + 24, cy + 24), fill=AZUL)
            d.text((cx, cy), str(i), font=F_NUM, fill="white", anchor="mm")
            d.text((x0 + 74, cy), nome, font=F_PASSO_ATUAL, fill=TEXTO, anchor="lm")
        else:
            d.ellipse((cx - 24, cy - 24, cx + 24, cy + 24), outline=(150, 185, 220), width=2)
            d.text((cx, cy), str(i), font=F_NUM, fill=MUTADO, anchor="mm")
            d.text((x0 + 74, cy), nome, font=F_PASSO, fill=MUTADO, anchor="lm")
        y += 84
    if legenda:
        linhas = quebrar(legenda, F_LEGENDA, 700)
        alto = 40 + 46 * len(linhas)
        vidro(img, (x0 - 14, 790, x0 + 760, 790 + alto), raio=24, alfa=190)
        d = ImageDraw.Draw(img)
        for k, linha in enumerate(linhas):
            d.text((x0 + 22, 810 + 46 * k), linha, font=F_LEGENDA, fill=TEXTO)


def cena_final(img, quadro, laudo, legenda):
    janela(img, quadro)
    alto = 900
    larg = int(laudo.width * alto / laudo.height)
    pdf = laudo.convert("RGB").resize((larg, alto), Image.LANCZOS)
    x = 1010 + (820 - larg) // 2
    com_sombra(img, pdf, (x, 50), raio=6)
    linhas = quebrar(legenda, F_LEGENDA, 820)
    vidro(img, (960, 968, 1880, 968 + 30 + 46 * len(linhas)), raio=24, alfa=210)
    d = ImageDraw.Draw(img)
    for k, linha in enumerate(linhas):
        d.text((1420, 983 + 46 * k + 18), linha, font=F_LEGENDA, fill=TEXTO, anchor="mm")


def main():
    pasta = sys.argv[1]
    cenas = os.path.join(pasta, "cenas")
    os.makedirs(cenas, exist_ok=True)
    base = fundo()
    linhas = [l.rstrip("\n").split("|") for l in open(os.path.join(pasta, "roteiro.txt"), encoding="utf-8") if l.strip()]
    saida, ultimo_quadro, anterior = [], None, None

    def gravar(img, ms):
        nome = f"c{len(saida) + 1:04d}.png"
        img.convert("RGB").save(os.path.join(cenas, nome), optimize=False, compress_level=1)
        saida.append(f"{nome}|{ms}")

    for arquivo, ms, passo, legenda in linhas:
        ms, passo = int(ms), int(passo)
        img = base.copy()
        if passo == 6:
            cena_final(img, ultimo_quadro, Image.open(os.path.join(pasta, arquivo)), legenda)
            # Transição suave para a cena final
            for k in range(1, 9):
                gravar(Image.blend(anterior, img, k / 9), 45)
        else:
            ultimo_quadro = Image.open(os.path.join(pasta, "bruto", arquivo)).convert("RGB")
            janela(img, ultimo_quadro)
            lado_direito(img, passo, legenda or "Do preenchimento ao PDF, em menos de um minuto. Dados fictícios.")
            if anterior is None:
                for k in range(0, 8):
                    gravar(Image.blend(base, img, k / 8), 45)
        gravar(img, ms)
        anterior = img

    open(os.path.join(cenas, "roteiro.txt"), "w", encoding="utf-8").write("\n".join(saida) + "\n")
    print(f"{len(saida)} cenas em {cenas}")


if __name__ == "__main__":
    main()
