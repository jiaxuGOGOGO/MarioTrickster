using System;
namespace UnityEngine
{
    [AttributeUsage(AttributeTargets.Field)] public sealed class SerializeField : Attribute { }
    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 operator +(Vector2 a, Vector2 b) { return new Vector2(a.x + b.x, a.y + b.y); }
        public static Vector2 operator -(Vector2 a, Vector2 b) { return new Vector2(a.x - b.x, a.y - b.y); }
        public static Vector2 operator *(Vector2 a, float b) { return new Vector2(a.x * b, a.y * b); }
        public static Vector2 operator /(Vector2 a, float b) { return new Vector2(a.x / b, a.y / b); }
    }
    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b, float a = 1f) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static Color black { get { return new Color(0, 0, 0, 1); } }
        public static Color white { get { return new Color(1, 1, 1, 1); } }
        public static Color Lerp(Color a, Color b, float t) { return new Color(a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t, a.b + (b.b - a.b) * t, a.a + (b.a - a.a) * t); }
    }
    public struct Rect
    {
        public float x, y, width, height;
        public Rect(float x, float y, float width, float height) { this.x = x; this.y = y; this.width = width; this.height = height; }
        public float xMax { get { return x + width; } }
        public float yMax { get { return y + height; } }
        public bool Contains(Vector2 p) { return p.x >= x && p.x < xMax && p.y >= y && p.y < yMax; } // same as UnityEngine.Rect
    public bool Overlaps(Rect other) { return x < other.xMax && xMax > other.x && y < other.yMax && yMax > other.y; }
    }
    public enum TextAnchor { MiddleCenter }
    public enum EventType { MouseMove, MouseDown, MouseDrag, ScrollWheel }
    public sealed class Event
    {
        public static Event current;
        public EventType type; public Vector2 mousePosition, delta; public int button; public bool control, command;
        public void Use() { }
    }
    public sealed class GUIContent
    {
        public GUIContent(string text) { }
        public GUIContent(string text, string tooltip) { }
        public static GUIContent none { get { return new GUIContent(""); } }
    }
    public sealed class GUIStyleState { public Color textColor; }
    public class GUIStyle
    {
        public GUIStyle() { normal = new GUIStyleState(); }
        public GUIStyle(GUIStyle other) { normal = new GUIStyleState(); }
        public TextAnchor alignment; public GUIStyleState normal;
    }
    public static class GUI
    {
        public static bool changed;
        public static void Box(Rect position, GUIContent content, GUIStyle style) { }
        public static void Label(Rect position, string text, GUIStyle style) { }
    }
    public static class GUILayout
    {
        public static void Space(float pixels) { }
        public static void FlexibleSpace() { }
        public static bool Button(string text, GUIStyle style, params object[] options) { return false; }
        public static bool Button(string text, params object[] options) { return false; }
        public static bool Toggle(bool value, string text, string style, params object[] options) { return value; }
        public static bool Toggle(bool value, GUIContent content, string style, params object[] options) { return value; }
        public static object Width(float width) { return null; }
        public static object ExpandWidth(bool expand) { return null; }
    }
    public static class GUILayoutUtility { public static Rect GetRect(float width, float height, params object[] options) { return new Rect(); } }
    public static class Mathf
    {
        public static float Clamp(float value, float min, float max) { return Math.Min(max, Math.Max(min, value)); }
        public static int Clamp(int value, int min, int max) { return Math.Min(max, Math.Max(min, value)); }
        public static int FloorToInt(float value) { return (int)Math.Floor(value); }
        public static int CeilToInt(float value) { return (int)Math.Ceiling(value); }
        public static int RoundToInt(float value) { return (int)Math.Round(value); }
        public static float Max(float a, float b) { return Math.Max(a, b); }
    }
}
namespace UnityEditor
{
    using UnityEngine;
    public static class EditorStyles
    {
        public static GUIStyle helpBox = new GUIStyle();
        public static GUIStyle boldLabel = new GUIStyle();
        public static GUIStyle miniButton = new GUIStyle();
        public static GUIStyle miniLabel = new GUIStyle();
        public static GUIStyle miniBoldLabel = new GUIStyle();
        public static GUIStyle centeredGreyMiniLabel = new GUIStyle();
        public static GUIStyle wordWrappedMiniLabel = new GUIStyle();
    }
    public static class EditorGUILayout
    {
        public static void LabelField(string label, GUIStyle style, params object[] options) { }
        public static void HelpBox(string message, MessageType type) { }
        public static void BeginHorizontal() { }
        public static void EndHorizontal() { }
        public static void BeginVertical(GUIStyle style) { }
        public static void EndVertical() { }
        public static string TextField(string label, string text) { return text; }
    public static string TextField(string text) { return text; } // Unity: TextField(string, params GUILayoutOption[])
        public static int IntField(string label, int value) { return value; }
        public static int Popup(string label, int selectedIndex, string[] displayedOptions) { return selectedIndex; }
    }
    public enum MessageType { None, Info, Warning, Error }
    public static class EditorGUI
    {
        public static void BeginDisabledGroup(bool disabled) { }
        public static void EndDisabledGroup() { }
        public static void DrawRect(Rect position, Color color) { }
    }
    public static class GUIUtility { public static void ExitGUI() { } }
}
namespace NUnit.Framework
{
    [AttributeUsage(AttributeTargets.Method)] public sealed class TestAttribute : Attribute { }
    public static class Assert
    {
        public static void IsTrue(bool value, string message = "") { if (!value) throw new Exception("Assert.IsTrue " + message); }
        public static void IsFalse(bool value, string message = "") { if (value) throw new Exception("Assert.IsFalse " + message); }
        public static void AreEqual<T>(T expected, T actual, string message = "") { if (!object.Equals(expected, actual)) throw new Exception("Assert.AreEqual " + expected + " != " + actual + " " + message); }
    }
    public static class StringAssert
    {
        public static void Contains(string expected, string actual) { if (actual == null || actual.IndexOf(expected, StringComparison.Ordinal) < 0) throw new Exception("StringAssert.Contains " + expected + " in " + actual); }
    }
}
