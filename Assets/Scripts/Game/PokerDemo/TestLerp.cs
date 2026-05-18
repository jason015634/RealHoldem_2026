using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public struct TestStruct
{
    public int a;
    public float b;
    public Vector3 c;
    public string s;
}

public class TestLerp : MonoBehaviour
{
    public List<TestStruct> testList = new List<TestStruct>();

    public void Start()
    {
        TestStruct s1 = new TestStruct { a = 1, b = 2.0f, c = Vector3.one, s = "Hello" };
        TestStruct s2 = new TestStruct { a = 10, b = 20.0f, c = Vector3.zero, s = "World" };
        TestStruct s3 = new TestStruct { a = 100, b = 200.0f, c = Vector3.up, s = "!" };
        TestStruct s4 = new TestStruct { a = 1000, b = 2000.0f, c = Vector3.down, s = "Test" };
        TestStruct s5 = new TestStruct { a = 1000, b = 20000.0f, c = Vector3.left, s = "Lerp" };

        testList.Add(s1);
        testList.Add(s2);
        testList.Add(s3);
        testList.Add(s4);
        testList.Add(s5);

        List<int> temp = testList.Select(s => s.a).Where(a => a > 10).Distinct().ToList();

        foreach(int i in temp)
        {
            Debug.Log(i);
        }
    }
}
