using System.Collections.Generic;
namespace UnityEngine.InputSystem {
  public class ButtonControl { public bool isPressed; public bool wasPressedThisFrame; }
  public class StickControl { public Vector2 ReadValue() => default; }
  public class DpadControl { public ButtonControl down, up, left, right; }
  public class Gamepad { public static IReadOnlyList<Gamepad> all => new List<Gamepad>();
    public ButtonControl buttonEast, buttonSouth, buttonNorth, buttonWest, leftShoulder, rightShoulder; public DpadControl dpad; public StickControl leftStick, rightStick; }
  // S197 stub: mirrors the real Input System Keyboard API used by Step1Keys / Vent
  public class KeyControl : ButtonControl {}
  public class Keyboard { public static Keyboard current;
    public KeyControl aKey,bKey,cKey,dKey,eKey,fKey,gKey,hKey,iKey,jKey,kKey,lKey,mKey,nKey,oKey,pKey,qKey,rKey,sKey,tKey,uKey,vKey,wKey,xKey,yKey,zKey,
      tabKey,spaceKey,shiftKey,downArrowKey,upArrowKey,leftArrowKey,rightArrowKey,escapeKey,enterKey,numpadEnterKey; }
}
namespace UnityEngine.UI {
  public class Graphic : MonoBehaviour { public Color color; public bool raycastTarget; public RectTransform rectTransform => null; }
  public class Text : Graphic { public string text; public int fontSize; public Font font; public TextAnchor alignment; public FontStyle fontStyle;
    public HorizontalWrapMode horizontalOverflow; public VerticalWrapMode verticalOverflow; public float lineSpacing; public bool supportRichText; }
  public class Image : Graphic { public Sprite sprite; }
  public class Button : MonoBehaviour {}
  public class GraphicRaycaster : MonoBehaviour {}
  public class CanvasScaler : MonoBehaviour { public enum ScaleMode { ConstantPixelSize, ScaleWithScreenSize, ConstantPhysicalSize } public enum ScreenMatchMode { MatchWidthOrHeight, Expand, Shrink }
    public ScaleMode uiScaleMode; public Vector2 referenceResolution; public ScreenMatchMode screenMatchMode; public float matchWidthOrHeight; }
}
