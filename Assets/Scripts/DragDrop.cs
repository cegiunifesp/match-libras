using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.U2D;
using UnityEngine.UI;

public class DragDrop : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IEndDragHandler, IDragHandler, IPointerClickHandler
{
    public Image imageTipo;
    private Vector3 originalScale;
    public float scaleMultiplier = 1.2f;

    private Canvas canvas;
    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private Vector2 lastMouseLocalPos;

    // =========================================================
    // DOUBLE TAP / DOUBLE CLICK
    // =========================================================

    [Header("Double Tap")]
    [Tooltip("Tempo máximo entre os dois toques/cliques.")]
    [SerializeField] private float intervaloDoubleTap = 0.3f;

    [Tooltip("Distância máxima que o dedo/mouse pode se mover para ainda ser considerado um toque.")]
    [SerializeField] private float distanciaMaximaToque = 20f;

    private float ultimoToque = -10f;
    private Vector2 posicaoPointerDown;
    private bool arrastou;

    [Header("Tipo da Peca")]
    public TipoPeca tipoPeca;

    [HideInInspector] public ValorNumero valorNumero;
    [HideInInspector] public ValorComida valorComida;

    public int Valor => tipoPeca == TipoPeca.Numero
        ? (int)valorNumero
        : (int)valorComida;

    [field: SerializeField] public List<Sprite> NumeroSprites { get; private set; }
    [field: SerializeField] public List<Sprite> ComidasSprites { get; private set; }

    public Vector2 PosicaoOriginal { get; private set; }

    private Transform parentOriginal;
    private bool foiTratadoNoDrop;
    private bool foiAceitoNoSlot;

    // Variáveis para gerenciar o Canvas interno e partículas
    private Canvas canvasInterno;
    private int ordemOriginalCanvasInterno;
    private ParticleSystemRenderer dustRenderer;
    private int ordemOriginalDust;

    [Header("Sombra")]
    public string nomeSombra = "Sombra";
    public Image imagemSombra;
    private float alphaOriginalSombra;

    public ParticleSystem particulasAcerto;

    private Coroutine escalaCoroutine;

    // =========================================================
    // POSIÇÃO ORIGINAL
    // =========================================================

    public void DefinirPosicaoOriginal()
    {
        PosicaoOriginal = rectTransform.anchoredPosition;
    }

    // =========================================================
    // DOUBLE TAP / DOUBLE CLICK
    // =========================================================

    public void OnPointerDown(PointerEventData eventData)
    {
        posicaoPointerDown = eventData.position;
        arrastou = false;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // Se houve um drag de verdade, não considera como tap.
        if (arrastou)
            return;

        float agora = Time.unscaledTime;

        // Segundo toque dentro do intervalo permitido.
        if (agora - ultimoToque <= intervaloDoubleTap)
        {
            // Reseta para impedir um terceiro toque de ser
            // considerado o início de outro double tap imediatamente.
            ultimoToque = -10f;

            ExecutarDoubleTap();
            return;
        }

        // Primeiro toque.
        ultimoToque = agora;
    }

    private void ExecutarDoubleTap()
    {
        var slotAtual = GetComponentInParent<ItemSlot>();

        // =====================================================
        // A PEÇA JÁ ESTÁ EM UM SLOT
        // Remove e devolve para a origem.
        // =====================================================

        if (slotAtual != null)
        {
            slotAtual.RemoverDoSlot(this);

            SoundManager.Instance?.Play("Drag_1");

            VoltarParaOrigem(0.2f, this);

            return;
        }

        // =====================================================
        // A PEÇA NÃO ESTÁ EM UM SLOT
        // Procura um slot válido e envia para ele.
        // =====================================================

        ItemSlot[] slots =
            FindObjectsByType<ItemSlot>(FindObjectsSortMode.None);

        foreach (var slot in slots)
        {
            if (slot.PodeAceitarPeca(this))
            {
                SoundManager.Instance?.Play("Drag_1");

                parentOriginal = transform.parent;

                slot.AutoEncaixarPeca(this);

                break;
            }
        }
    }

    // =========================================================
    // PARENT ORIGINAL
    // =========================================================

    // Chamado por LevelManager ao spawnar a peca, garante que
    // parentOriginal está correto mesmo antes do primeiro drag.
    public void DefinirParentOriginal(Transform parent)
    {
        parentOriginal = parent;
    }

    // =========================================================
    // DROP
    // =========================================================

    // Chamado por ItemSlot quando REJEITA a peca.
    public void MarcarTratadoNoDrop()
    {
        foiTratadoNoDrop = true;
    }

    // Chamado por ItemSlot quando ACEITA a peca.
    public void MarcarAceitoNoSlot()
    {
        foiTratadoNoDrop = true;
        foiAceitoNoSlot = true;
    }

    // =========================================================
    // VOLTAR PARA ORIGEM
    // =========================================================

    // Anima a peca de volta para PosicaoOriginal dentro do
    // parentOriginal.
    // Pausa a física durante a animação e a retoma ao terminar.
    public void VoltarParaOrigem(float duracao, MonoBehaviour runner)
    {
        // Reparenta para o container correto (containerPecas),
        // não para o canvas raiz.
        //
        // PosicaoOriginal está em espaço de containerPecas,
        // então o parent precisa ser o mesmo.

        Transform alvo = parentOriginal != null
            ? parentOriginal
            : runner.GetComponentInParent<Canvas>().transform;

        transform.SetParent(alvo, true);

        GetComponent<CanvasGroup>().blocksRaycasts = false;

        // Pausa física para não conflitar com a animação de volta.
        GetComponent<PecaFisica>()?.PausarFisica();

        runner.StartCoroutine(AnimarVolta(duracao));
    }

    private IEnumerator AnimarVolta(float duracao)
    {
        Vector2 atual = rectTransform.anchoredPosition;

        float tempo = 0f;

        while (tempo < duracao)
        {
            tempo += Time.deltaTime;

            float t = Mathf.SmoothStep(
                0f,
                1f,
                tempo / duracao
            );

            rectTransform.anchoredPosition =
                Vector2.Lerp(
                    atual,
                    PosicaoOriginal,
                    t
                );

            yield return null;
        }

        rectTransform.anchoredPosition = PosicaoOriginal;

        canvasGroup.blocksRaycasts = true;

        // Retoma física sem impulso.
        // A peça chegou na origem e fica quieta.
        GetComponent<PecaFisica>()?.RetomarFisica(Vector2.zero);
    }

    // =========================================================
    // AWAKE
    // =========================================================

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();

        canvasGroup = GetComponent<CanvasGroup>();

        canvas = GetComponentInParent<Canvas>();

        originalScale = rectTransform.localScale;

        var sombraTransform = transform.Find(nomeSombra);

        if (sombraTransform != null)
        {
            imagemSombra = sombraTransform.GetComponent<Image>();

            alphaOriginalSombra = imagemSombra.color.a;
        }
    }

    // =========================================================
    // SPRITE
    // =========================================================

    public void AplicarSprite()
    {
        if (tipoPeca == TipoPeca.Numero)
        {
            imageTipo.sprite =
                NumeroSprites[(int)valorNumero];
        }
        else
        {
            imageTipo.sprite =
                ComidasSprites[(int)valorComida];
        }
    }

    // =========================================================
    // BEGIN DRAG
    // =========================================================

    public void OnBeginDrag(PointerEventData eventData)
    {
        var slot = GetComponentInParent<ItemSlot>();

        if (slot != null)
        {
            slot.RemoverDoSlot(this);
        }

        parentOriginal = transform.parent;

        foiTratadoNoDrop = false;
        foiAceitoNoSlot = false;

        if (escalaCoroutine != null)
        {
            StopCoroutine(escalaCoroutine);
        }

        escalaCoroutine = StartCoroutine(
            AnimarEscalaESombra(
                originalScale * scaleMultiplier,
                1f,
                0.15f
            )
        );

        canvasGroup.blocksRaycasts = false;

        GetComponent<PecaFisica>()?.PausarFisica();

        SoundManager.Instance?.Play("Drag_1");

        rectTransform.SetAsLastSibling();

        rectTransform.anchoredPosition3D =
            new Vector3(
                rectTransform.anchoredPosition.x,
                rectTransform.anchoredPosition.y,
                0f
            );

        // =====================================================
        // 1. CANVAS RAIZ
        // Afeta a sombra
        // =====================================================

        Canvas canvasLocal = GetComponent<Canvas>();

        if (canvasLocal == null)
        {
            canvasLocal = gameObject.AddComponent<Canvas>();
        }

        canvasLocal.overrideSorting = true;

        canvasLocal.sortingOrder = 4;

        // =====================================================
        // 2. CANVAS INTERNO
        // Afeta Base e Tipo
        // =====================================================

        canvasInterno =
            transform.Find("Canvas")?.GetComponent<Canvas>();

        if (canvasInterno != null)
        {
            ordemOriginalCanvasInterno =
                canvasInterno.sortingOrder;

            canvasInterno.sortingOrder = 5;
        }

        // =====================================================
        // 3. PARTÍCULAS
        // Dust
        // =====================================================

        Transform dustTransform = transform.Find("Dust");

        if (dustTransform != null)
        {
            dustRenderer =
                dustTransform.GetComponent<ParticleSystemRenderer>();

            if (dustRenderer != null)
            {
                ordemOriginalDust =
                    dustRenderer.sortingOrder;

                dustRenderer.sortingOrder = 985;
            }
        }

        StartCoroutine(
            AnimarRotacao(
                Quaternion.identity,
                0.15f
            )
        );

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.GetComponent<RectTransform>(),
            eventData.position,
            canvas.worldCamera,
            out lastMouseLocalPos
        );
    }

    // =========================================================
    // DRAG
    // =========================================================

    public void OnDrag(PointerEventData eventData)
    {
        // Detecta se o usuário realmente arrastou.
        //
        // Pequenos movimentos do dedo são ignorados para que
        // ainda possam contar como tap.
        if (!arrastou)
        {
            float distancia = Vector2.Distance(
                posicaoPointerDown,
                eventData.position
            );

            if (distancia > distanciaMaximaToque)
            {
                arrastou = true;

                // Também invalida um possível primeiro tap
                // anterior, evitando:
                //
                // tap -> drag -> tap
                //
                // ser interpretado como double tap.
                ultimoToque = -10f;
            }
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.GetComponent<RectTransform>(),
            eventData.position,
            canvas.worldCamera,
            out Vector2 currentMouseLocalPos
        );

        Vector2 delta =
            currentMouseLocalPos - lastMouseLocalPos;

        rectTransform.anchoredPosition += delta;

        lastMouseLocalPos = currentMouseLocalPos;
    }

    // =========================================================
    // END DRAG
    // =========================================================

    public void OnEndDrag(PointerEventData eventData)
    {
        if (escalaCoroutine != null)
        {
            StopCoroutine(escalaCoroutine);
        }

        escalaCoroutine = StartCoroutine(
            AnimarEscalaESombra(
                originalScale,
                alphaOriginalSombra,
                0.15f
            )
        );

        canvasGroup.blocksRaycasts = true;

        rectTransform.SetAsLastSibling();

        rectTransform.anchoredPosition3D =
            new Vector3(
                rectTransform.anchoredPosition.x,
                rectTransform.anchoredPosition.y,
                0f
            );

        // =====================================================
        // REMOVE O CANVAS TEMPORÁRIO DA RAIZ
        // =====================================================

        Canvas canvasLocal = GetComponent<Canvas>();

        if (canvasLocal != null)
        {
            Destroy(canvasLocal);
        }

        // =====================================================
        // RESTAURA O CANVAS INTERNO
        // =====================================================

        if (canvasInterno != null)
        {
            canvasInterno.sortingOrder =
                ordemOriginalCanvasInterno;
        }

        // =====================================================
        // RESTAURA AS PARTÍCULAS
        // =====================================================

        if (dustRenderer != null)
        {
            dustRenderer.sortingOrder =
                ordemOriginalDust;
        }

        // =====================================================
        // DROP NÃO FOI TRATADO
        // =====================================================

        if (!foiTratadoNoDrop)
        {
            Transform alvo =
                parentOriginal != null
                    ? parentOriginal
                    : GetComponentInParent<Canvas>().transform;

            transform.SetParent(alvo, true);

            DefinirPosicaoOriginal();

            GetComponent<PecaFisica>()?.RetomarFisica(
                eventData.delta * 3f
            );
        }

        // =====================================================
        // PEÇA FOI ACEITA
        // =====================================================

        else if (foiAceitoNoSlot)
        {
            GetComponent<PecaFisica>()?.RetomarFisica(
                eventData.delta * 3f
            );

            if (particulasAcerto != null)
            {
                particulasAcerto.Play();
            }
        }
    }

    // =========================================================
    // ANIMAÇÃO ESCALA + SOMBRA
    // =========================================================

    private IEnumerator AnimarEscalaESombra(
        Vector3 escalaAlvo,
        float alphaAlvo,
        float duracao
    )
    {
        Vector3 escalaInicial =
            rectTransform.localScale;

        float alphaInicial =
            imagemSombra != null
                ? imagemSombra.color.a
                : 0f;

        float tempo = 0f;

        while (tempo < duracao)
        {
            tempo += Time.deltaTime;

            float t =
                EaseInOut(tempo / duracao);

            rectTransform.localScale =
                Vector3.Lerp(
                    escalaInicial,
                    escalaAlvo,
                    t
                );

            if (imagemSombra != null)
            {
                Color c = imagemSombra.color;

                c.a = Mathf.Lerp(
                    alphaInicial,
                    alphaAlvo,
                    t
                );

                imagemSombra.color = c;
            }

            yield return null;
        }

        rectTransform.localScale = escalaAlvo;
    }

    // =========================================================
    // ANIMAÇÃO ROTAÇÃO
    // =========================================================

    public IEnumerator AnimarRotacao(
        Quaternion alvo,
        float duracao
    )
    {
        Quaternion inicial =
            rectTransform.rotation;

        float tempo = 0f;

        while (tempo < duracao)
        {
            tempo += Time.deltaTime;

            float t =
                EaseInOut(tempo / duracao);

            rectTransform.rotation =
                Quaternion.Lerp(
                    inicial,
                    alvo,
                    t
                );

            yield return null;
        }

        rectTransform.rotation = alvo;
    }

    // =========================================================
    // EASING
    // =========================================================

    private float EaseInOut(float t)
    {
        return t * t * (3f - 2f * t);
    }
}