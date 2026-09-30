using System;
namespace UnityEngine {
  public class Object { public HideFlags hideFlags; public string name; public static void DestroyImmediate(Object o){} public static implicit operator bool(Object o)=>!ReferenceEquals(o,null);}
  public class ScriptableObject : Object { public static T CreateInstance<T>() where T: ScriptableObject, new() => new T(); }
  public class MonoBehaviour : Object {}
  public enum HideFlags { None=0, HideAndDontSave=61 }
  public static class Resources { public static T Load<T>(string p) where T:Object => null; }
  public static class Debug { public static void Log(object o){} public static void LogWarning(object o){} public static void LogError(object o){ Console.WriteLine("LOGERR "+o);} }
  public static class GUIUtility { public static string systemCopyBuffer; }
  public static class Mathf {
    public static int FloorToInt(float f)=>(int)Math.Floor(f); public static int CeilToInt(float f)=>(int)Math.Ceiling(f);
    public static float Min(float a,float b)=>Math.Min(a,b); public static int Min(int a,int b)=>Math.Min(a,b);
    public static float Max(float a,float b)=>Math.Max(a,b); public static int Max(int a,int b)=>Math.Max(a,b);
    public static float Abs(float a)=>Math.Abs(a); public static int Abs(int a)=>Math.Abs(a);
    public static float Sqrt(float a)=>(float)Math.Sqrt(a); public static float Clamp(float v,float a,float b)=>v<a?a:v>b?b:v; public static int Clamp(int v,int a,int b)=>v<a?a:v>b?b:v;
    public static float Clamp01(float v)=>Clamp(v,0,1); public static float Pow(float a,float b)=>(float)Math.Pow(a,b); public static int RoundToInt(float f)=>(int)Math.Round(f); public static float Sign(float f)=>f>=0?1:-1; public static float MoveTowards(float c,float t,float d)=>Math.Abs(t-c)<=d?t:c+Math.Sign(t-c)*d; public static float Lerp(float a,float b,float t)=>a+(b-a)*Clamp01(t); }
  public struct Vector2 { public float x,y; public Vector2(float x,float y){this.x=x;this.y=y;} public static Vector2 zero=>new Vector2(0,0); public static Vector2 one=>new Vector2(1,1);
    public static Vector2 operator*(Vector2 a,float b)=>new Vector2(a.x*b,a.y*b); public static Vector2 operator+(Vector2 a,Vector2 b)=>new Vector2(a.x+b.x,a.y+b.y);
    public static Vector2 operator-(Vector2 a,Vector2 b)=>new Vector2(a.x-b.x,a.y-b.y); public static Vector2 operator/(Vector2 a,float b)=>new Vector2(a.x/b,a.y/b);
    public float magnitude=>(float)Math.Sqrt(x*x+y*y); public static float Distance(Vector2 a,Vector2 b)=>(a-b).magnitude; public static Vector2 right=>new Vector2(1,0); public static Vector2 left=>new Vector2(-1,0);
    public static bool operator==(Vector2 a,Vector2 b)=>a.x==b.x&&a.y==b.y; public static bool operator!=(Vector2 a,Vector2 b)=>!(a==b); public override bool Equals(object o)=>o is Vector2 v&&v==this; public override int GetHashCode()=>0; public override string ToString()=>$"({x},{y})";}
  public struct Color { public float r,g,b,a; public static bool operator==(Color x,Color y)=>x.r==y.r&&x.g==y.g&&x.b==y.b&&x.a==y.a; public static bool operator!=(Color x,Color y)=>!(x==y); public override bool Equals(object o)=>o is Color c&&c==this; public override int GetHashCode()=>0; public Color(float r,float g,float b,float a=1){this.r=r;this.g=g;this.b=b;this.a=a;} public static Color white=>new Color(1,1,1);}
  public class HeaderAttribute:Attribute{public HeaderAttribute(string s){}} public class TooltipAttribute:Attribute{public TooltipAttribute(string s){}}
  public class RangeAttribute:Attribute{public RangeAttribute(float a,float b){}} public class SerializeField:Attribute{}
  public class CreateAssetMenuAttribute:Attribute{public string fileName,menuName;public int order;} public class SpaceAttribute:Attribute{public SpaceAttribute(){} public SpaceAttribute(float f){}}
  public class TextAreaAttribute:Attribute{public TextAreaAttribute(){} public TextAreaAttribute(int a,int b){}}
}
namespace UnityEngine { public class Transform {} }

namespace UnityEngine { public struct Vector2Int { public int x,y; public Vector2Int(int x,int y){this.x=x;this.y=y;} } }
// S213：玩家视角模拟需要的桩（Step1Text / OverworldSession 编进 sim）
namespace UnityEngine { public enum RuntimeInitializeLoadType { SubsystemRegistration, AfterAssembliesLoaded, BeforeSceneLoad, AfterSceneLoad } public class RuntimeInitializeOnLoadMethodAttribute:Attribute{ public RuntimeInitializeOnLoadMethodAttribute(){} public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType t){} } }
public enum MarioMindState { Running, Curious, Investigating, Chasing, Searching }
