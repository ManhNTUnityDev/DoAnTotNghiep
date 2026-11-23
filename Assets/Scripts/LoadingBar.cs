using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.SceneManagement;
using DG.Tweening;
using ChaseGame.Infrastructure;

public class LoadingBar : MonoBehaviour
{
    [SerializeField] private Image loadingFill;

    private AsyncOperation asyncOperation;
    
    private const float LoadingTime = 3f;

    private void Start()
    {
        StartCoroutine(LoadSceneAsync());
    }

    private IEnumerator LoadSceneAsync()
    {
        yield return null;

        asyncOperation = SceneManager.LoadSceneAsync(SceneNames.Menu);
        asyncOperation.allowSceneActivation = false;

        loadingFill.DOFillAmount(1, LoadingTime).SetEase(Ease.Linear).OnComplete(() =>
        {
            asyncOperation.allowSceneActivation = true;
        });
    }
}
