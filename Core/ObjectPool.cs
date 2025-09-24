using System.Collections.Generic;
using UnityEngine;

// 오브젝트 풀링 시스템
// 게임 오브젝트의 생성과 파괴를 최소화하여 성능 최적화
public class ObjectPool<T> where T : UnityEngine.Object
{
    private readonly Stack<T> _pool = new Stack<T>();
    private readonly T _prefab;
    private readonly Transform _parent;

    public ObjectPool(T prefab, int initialSize, Transform parent = null)
    {
        _prefab = prefab;
        _parent = parent;

        for (int i = 0; i < initialSize; i++)
        {
            T obj = Object.Instantiate(prefab, parent);
            // T가 GameObject 또는 Component일 경우에만 SetActive 호출
            if (obj is GameObject go) go.SetActive(false);
            else if (obj is Component comp) comp.gameObject.SetActive(false);
            _pool.Push(obj);
        }
    }

    public T Get()
    {
        T obj;
        while (_pool.Count > 0)
        {
            obj = _pool.Pop();
            if (obj) // 유효성 검사 수정
            {
                // T가 GameObject 또는 Component일 경우에만 SetActive 호출
                if (obj is GameObject go1) go1.SetActive(true);
                else if (obj is Component comp1) comp1.gameObject.SetActive(true);
                return obj;
            }
        }
        obj = Object.Instantiate(_prefab, _parent);
        // T가 GameObject 또는 Component일 경우에만 SetActive 호출
        if (obj is GameObject go2) go2.SetActive(true);
        else if (obj is Component comp2) comp2.gameObject.SetActive(true);
        return obj;
    }

    public void Return(T obj)
    {
        if (obj) // 유효성 검사 추가
        {
            // T가 GameObject 또는 Component일 경우에만 SetActive 호출
            if (obj is GameObject go) go.SetActive(false);
            else if (obj is Component comp) comp.gameObject.SetActive(false);
            _pool.Push(obj);
        }
    }
}