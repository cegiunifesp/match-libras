using UnityEngine;

public class SafeArea : MonoBehaviour
{
    [SerializeField] private float paddingLeft = 20f;
    [SerializeField] private float paddingRight = 20f;
    [SerializeField] private float paddingTop = 20f;
    [SerializeField] private float paddingBottom = 20f;

    private void Start()
    {
        Rect safeArea = Screen.safeArea;

        safeArea.xMin += paddingLeft;
        safeArea.xMax -= paddingRight;
        safeArea.yMin += paddingBottom;
        safeArea.yMax -= paddingTop;

        RectTransform rect = GetComponent<RectTransform>();

        rect.anchorMin = new Vector2(
            safeArea.xMin / Screen.width,
            safeArea.yMin / Screen.height
        );

        rect.anchorMax = new Vector2(
            safeArea.xMax / Screen.width,
            safeArea.yMax / Screen.height
        );

        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}