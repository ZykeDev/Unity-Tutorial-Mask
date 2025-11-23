using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Noya.TutorialMask
{
	[ExecuteAlways, RequireComponent(typeof(Graphic))]
	public class TutorialMask : MonoBehaviour
	{
		private const string SHADER_NAME = "UI/Noya/TutorialMask/Circle Hole Mask";
		private static readonly int ShaderPropCenter = Shader.PropertyToID("_HoleCenter");
		private static readonly int ShaderPropRadius = Shader.PropertyToID("_HoleRadius");
		private static readonly int ShaderPropAspectRatio = Shader.PropertyToID("_ParentAspectRatio");
		private static readonly int ShaderPropFadeDistance = Shader.PropertyToID("_FadeDistance");
		
		[SerializeField, Tooltip("Can also be set with SetTarget(RectTransform).")] private RectTransform target;
		[SerializeField, Range(0, 1), Tooltip("Can also be set with SetOpacity(float).")] private float targetBackgroundOpacity = 0.8f;
		[SerializeField, Min(0), Tooltip("Can also be set with SetRadius(float).")] private float targetHoleRadius = 1f;
		[SerializeField, Range(0, 1), Tooltip("The size of the faded border around the circle. Can also be set with SetFadeDistance(float).")] private float fadeDistance = 0.1f;

		private Graphic graphic;
		private RectTransform canvasRect;
		private Material materialInstance;
		private Vector2 canvasSize;

		private bool useAnimation;
		private float animationDuration;
		private float currentRadius;
		private float currentOpacity;
		
		
		private void Awake()
		{
			graphic = GetComponent<Graphic>();
		
			// Check for the correct shader
			if (graphic.material && 
			    graphic.material.shader && 
			    graphic.material.shader.name != SHADER_NAME)
			{
				Debug.LogError($"The Graphic component on this GameObject does not have the '{SHADER_NAME}' shader applied.");
				enabled = false;
				return;
			}

			// Create a material instance to prevent changing the global shader asset
			CreateMaterialInstance();

			// Automatically try to find the root canvas RectTransform
			Canvas rootCanvas = GetComponentInParent<Canvas>();
			if (rootCanvas)
			{
				canvasRect = rootCanvas.GetComponent<RectTransform>();
			}
			else
			{
				Debug.LogError("Canvas RectTransform not found. Please assign it manually or ensure the object is inside a Canvas.");
			}
		}
		
		private void Update()
		{
			if (!graphic || !graphic.enabled || !target || !canvasRect || !materialInstance)
				return;
			
			canvasSize = canvasRect.sizeDelta;
			float screenW = canvasSize.x;
			float screenH = canvasSize.y;
			
			// 1. Calculate the center of the target in normalized screen coordinates (0 to 1)
			
			// Get the camera used by the Canvas. This is crucial for WorldToScreenPoint.
			Camera canvasCamera = graphic.canvas.worldCamera;

			// Convert the target object's world position to the raw screen pixel position
			Vector3 worldPosition = target.position;
			Vector2 rawScreenPosition = RectTransformUtility.WorldToScreenPoint(canvasCamera, worldPosition);
			
			// Convert raw screen position to normalized screen UV (0 to 1)
			Vector4 centerUV = new Vector4(
				rawScreenPosition.x / screenW,
				rawScreenPosition.y / screenH,
				0, 0
			);

			// 2. Calculate the normalized radius squared in screen UV space

			// We project the center and a point offset by 'holeRadius' onto the screen
			Vector2 centerScreen = RectTransformUtility.WorldToScreenPoint(canvasCamera, worldPosition);
			// Project a point offset by holeRadius along the world's right axis
			Vector2 offsetScreen = RectTransformUtility.WorldToScreenPoint(canvasCamera, worldPosition + Vector3.right * currentRadius);
			// Calculate the distance in screen pixels
			float radiusInScreenPixels = Vector2.Distance(centerScreen, offsetScreen);
			// Normalize the radius by the screen width
			float uvSpaceRadius = radiusInScreenPixels / screenW; 
			
			// Apply the calculated values to the shader material
			graphic.color = new Color(graphic.color.r, graphic.color.g, graphic.color.b, currentOpacity);
			materialInstance.SetVector(ShaderPropCenter, centerUV);
			materialInstance.SetFloat(ShaderPropRadius, uvSpaceRadius);
			materialInstance.SetFloat(ShaderPropAspectRatio, screenW / screenH);
			materialInstance.SetFloat(ShaderPropFadeDistance, fadeDistance);
		}

		/// <summary>
		/// Enables the mask.
		/// </summary>
		/// <remarks>If <see cref="WithAnimation"/> as called on the object beforehand, it will enable it with an animation.
		/// Otherwise, enabling will be instant.</remarks>
		[ContextMenu("Enable")]
		public TutorialMask Enable()
		{
			graphic.enabled = true;

			if (useAnimation)
			{
				StopAllCoroutines();
				StartCoroutine(EnableAnimated());
			}
			else
			{
				currentRadius = targetHoleRadius;
				currentOpacity = targetBackgroundOpacity;
			}
			
			return this;
		}
		
		/// <summary>
		/// Disables the mask.
		/// </summary>
		/// <remarks>If <see cref="WithAnimation"/> was called on the object beforehand, it will disable it with an animation. Otherwise, disabling is instant.</remarks>
		[ContextMenu("Disable")]
		public TutorialMask Disable()
		{
			if (useAnimation)
			{
				StopAllCoroutines();
				StartCoroutine(DisableAnimated());
			}
			else
			{
				graphic.enabled = false;
			}
			
			useAnimation = false;
			return this;
		}
		
		/// <summary>
		/// Sets the Target around which the mask will be drawn.
		/// </summary>
		public TutorialMask SetTarget(RectTransform target)
		{
			this.target = target;
			return this;
		}
		
		/// <summary>
		/// Sets the opacity of the background around the mask.
		/// </summary>
		public TutorialMask SetOpacity(float opacity)
		{
			targetBackgroundOpacity = Mathf.Clamp01(opacity);
			graphic.color = new Color(graphic.color.r, graphic.color.g, graphic.color.b, targetBackgroundOpacity);
			return this;
		}

		/// <summary>
		/// Sets the radius of the mask circle.
		/// </summary>
		public TutorialMask SetRadius(float radius)
		{
			targetHoleRadius = radius;
			return this;
		}
		
		/// <summary>
		/// Sets the thickness (in pixels) of the faded perimeter around the mask.
		/// </summary>
		public TutorialMask SetFadeDistance(float fadeDistance)
		{
			this.fadeDistance = fadeDistance;
			return this;
		}

		/// <summary>
		/// Enables animation for the tutorial mask with the specified duration.
		/// </summary>
		/// <param name="duration">The duration in seconds.</param>
		public TutorialMask WithAnimation(float duration)
		{
			useAnimation = true;
			animationDuration = duration;
			return this;
		}

		private IEnumerator EnableAnimated()
		{
			const float startOpacity = 0f;
			float targetOpacity = targetBackgroundOpacity;
			float startRadius = canvasRect.sizeDelta.x / 2f;

			float timer = 0f;
			while (timer < animationDuration)
			{
				timer += Time.deltaTime;
				float t = Mathf.SmoothStep(0f, 1f, timer / animationDuration);
				
				currentRadius = Mathf.Lerp(startRadius, targetHoleRadius, t);
				currentOpacity = Mathf.Lerp(startOpacity, targetOpacity, t);
				yield return null;
			}

			currentRadius = targetHoleRadius;
			currentOpacity = targetOpacity;
		}
		
		private IEnumerator DisableAnimated()
		{
			float startRadius = currentRadius;
			float targetRadius = canvasRect.sizeDelta.x / 2f;
			const float targetOpacity = 0f;
			
			float timer = 0f;
			while (timer < animationDuration)
			{
				timer += Time.deltaTime;
				float t = Mathf.SmoothStep(0f, 1f, timer / animationDuration);
				
				currentRadius = Mathf.Lerp(startRadius, targetRadius, t);
				currentOpacity = Mathf.Lerp(currentOpacity, targetOpacity, t);
				yield return null;
			}

			graphic.enabled = false;
		}
		
		private void CreateMaterialInstance()
		{
			materialInstance = new Material(graphic.material);
			graphic.material = materialInstance;
		}

		private void OnDestroy()
		{
			// Clean up the material instance when destroyed
			if (materialInstance)
			{
				DestroyImmediate(materialInstance);
				graphic.material = null; 
			}
			
			StopAllCoroutines();
		}
	}
}
