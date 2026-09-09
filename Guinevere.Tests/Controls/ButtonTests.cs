// public class ButtonTests : PrimitiveControlsTestBase
// {
//     [Fact]
//     public void Button_BuildStage_CreatesNodeWithCorrectId()
//     {
//         // Arrange
//         var gui = CreateTestGui();
//         SimulateBuildStage(gui);
//
//         // Act
//         gui.Button("Test Button");
//
//         // Assert
//         var node = gui.RootNode?.Children.FirstOrDefault();
//         Assert.NotNull(node);
//         Assert.Contains("ButtonTests.cs", node.Id);
//     }
//
//     [Fact]
//     public void Button_WithAutoSizing_CalculatesCorrectDimensions()
//     {
//         // Arrange
//         var gui = CreateTestGui();
//         SimulateBuildStage(gui);
//
//         // Act
//         gui.Button("Test");
//
//         // Assert
//         var node = gui.RootNode?.Children.FirstOrDefault();
//         Assert.NotNull(node);
//         Assert.True(node.Rect.W >= 80); // Minimum width
//         Assert.True(node.Rect.H >= 32); // Minimum height
//     }
//
//     [Fact]
//     public void Button_WithSpecificDimensions_UsesProvidedSize()
//     {
//         // Arrange
//         var gui = CreateTestGui();
//         SimulateBuildStage(gui);
//         const float expectedWidth = 150f;
//         const float expectedHeight = 50f;
//
//         // Act
//         gui.Button("Test", width: expectedWidth, height: expectedHeight);
//
//         // Assert
//         var node = gui.RootNode?.Children.FirstOrDefault();
//         Assert.NotNull(node);
//         AssertNodeHasSize(node, expectedWidth, expectedHeight);
//     }
//
//     [Fact]
//     public void Button_NotClicked_ReturnsFalse()
//     {
//         // Arrange
//         var gui = CreateTestGui();
//
//         // Act & Assert
//         SimulateFullControlLifecycle(gui,
//             buildAction: () => { },
//             renderAction: () =>
//             {
//                 var result = gui.Button("Test Button");
//                 Assert.False(result);
//             });
//     }
//
//     [Fact]
//     public void Button_ClickedInsideBounds_ReturnsTrue()
//     {
//         // Arrange
//         var gui = CreateTestGui();
//         var input = gui.Input;
//         SetupMouseClick(input, new Vector2(50, 25)); // Click inside button bounds
//
//         // Act & Assert
//         SimulateFullControlLifecycle(gui,
//             buildAction: () => gui.Button("Test Button"),
//             renderAction: () =>
//             {
//                 var result = gui.Button("Test Button");
//                 Assert.True(result);
//             });
//     }
//
//     [Fact]
//     public void Button_ClickedOutsideBounds_ReturnsFalse()
//     {
//         // Arrange
//         var gui = CreateTestGui();
//         var input = gui.Input;
//         SetupMouseClick(input, new Vector2(200, 200)); // Click outside button bounds
//
//         // Act & Assert
//         SimulateFullControlLifecycle(gui,
//             buildAction: () => gui.Button("Test Button"),
//             renderAction: () =>
//             {
//                 var result = gui.Button("Test Button");
//                 Assert.False(result);
//             });
//     }
//
//     [Fact]
//     public void Button_HoveredInsideBounds_HandlesProperly()
//     {
//         // Arrange
//         var gui = CreateTestGui();
//         var input = gui.Input;
//         SetupMouseHover(input, new Vector2(50, 25)); // Hover inside button bounds
//
//         // Act & Assert
//         SimulateFullControlLifecycle(gui,
//             buildAction: () => gui.Button("Test Button"),
//             renderAction: () =>
//             {
//                 // Should not throw any exceptions during hover handling
//                 var result = gui.Button("Test Button");
//                 Assert.False(result); // No click, just hover
//             });
//     }
//
//     [Theory]
//     [InlineData(MouseButton.Left)]
//     [InlineData(MouseButton.Right)]
//     [InlineData(MouseButton.Middle)]
//     public void Button_ClickedWithDifferentMouseButtons_OnlyLeftClickReturnsTrue(MouseButton button)
//     {
//         // Arrange
//         var gui = CreateTestGui();
//         var input = gui.Input;
//         SetupMouseClick(input, new Vector2(50, 25), button);
//
//         // Act & Assert
//         SimulateFullControlLifecycle(gui,
//             buildAction: () => gui.Button("Test Button"),
//             renderAction: () =>
//             {
//                 var result = gui.Button("Test Button");
//                 Assert.Equal(button == MouseButton.Left, result);
//             });
//     }
//
//     [Fact]
//     public void Button_WithCustomColors_RendersCorrectly()
//     {
//         // Arrange
//         var gui = CreateTestGui();
//         var backgroundColor = Color.Red;
//         var hoverColor = Color.Green;
//         var pressedColor = Color.Blue;
//         var textColor = Color.White;
//
//         // Act & Assert
//         SimulateFullControlLifecycle(gui,
//             buildAction: () => { },
//             renderAction: () =>
//             {
//                 // Should handle custom colors without throwing
//                 var result = gui.Button("Test",
//                     backgroundColor: backgroundColor,
//                     hoverColor: hoverColor,
//                     pressedColor: pressedColor,
//                     textColor: textColor);
//                 Assert.False(result); // No interaction
//             });
//     }
//
//     [Fact]
//     public void Button_MultipleButtons_EachHasUniqueId()
//     {
//         // Arrange
//         var gui = CreateTestGui();
//         SimulateBuildStage(gui);
//
//         // Act
//         gui.Button("Button 1");
//         gui.Button("Button 2");
//         gui.Button("Button 3");
//
//         // Assert
//         Assert.Equal(3, gui.RootNode?.Children.Count);
//         var ids = gui.RootNode?.Children.Select(c => c.Id).ToArray();
//         Assert.NotNull(ids);
//         Assert.Equal(3, ids.Distinct().Count()); // All IDs should be unique
//     }
//
//     [Fact]
//     public void Button_CalledMultipleTimesWithSameParameters_MaintainsConsistentState()
//     {
//         // Arrange
//         var gui = CreateTestGui();
//
//         // Act & Assert - First call
//         SimulateFullControlLifecycle(gui,
//             buildAction: () => gui.Button("Consistent Button"),
//             renderAction: () =>
//             {
//                 var firstResult = gui.Button("Consistent Button");
//                 Assert.False(firstResult);
//             });
//
//         // Reset for second call
//         gui.EndFrame();
//         gui.BeginFrame(CreateTestCanvas());
//
//         // Act & Assert - Second call
//         SimulateFullControlLifecycle(gui,
//             buildAction: () => gui.Button("Consistent Button"),
//             renderAction: () =>
//             {
//                 var secondResult = gui.Button("Consistent Button");
//                 Assert.False(secondResult);
//             });
//     }
//
//     [Fact]
//     public void Button_WithEmptyText_HandlesGracefully()
//     {
//         // Arrange
//         var gui = CreateTestGui();
//
//         // Act & Assert
//         SimulateFullControlLifecycle(gui,
//             buildAction: () => { },
//             renderAction: () =>
//             {
//                 // Should handle empty text without issues
//                 var result1 = gui.Button("");
//                 var result2 = gui.Button(string.Empty);
//                 Assert.False(result1);
//                 Assert.False(result2);
//             });
//     }
//
//     [Fact]
//     public void Button_WithVeryLongText_HandlesGracefully()
//     {
//         // Arrange
//         var gui = CreateTestGui();
//         var longText = new string('A', 1000);
//
//         // Act & Assert
//         SimulateFullControlLifecycle(gui,
//             buildAction: () => { },
//             renderAction: () =>
//             {
//                 // Should handle very long text without issues
//                 var result = gui.Button(longText);
//                 Assert.False(result);
//             });
//     }
//
//     [Theory]
//     [InlineData(0f, 0f)] // Zero dimensions
//     [InlineData(-10f, -5f)] // Negative dimensions
//     [InlineData(1000f, 1000f)] // Very large dimensions
//     [InlineData(0.1f, 0.1f)] // Very small dimensions
//     public void Button_WithEdgeCaseDimensions_HandlesGracefully(float width, float height)
//     {
//         // Arrange
//         var gui = CreateTestGui();
//
//         // Act & Assert
//         SimulateFullControlLifecycle(gui,
//             buildAction: () => { },
//             renderAction: () =>
//             {
//                 // Should handle edge case dimensions without throwing
//                 var result = gui.Button("Test", width: width, height: height);
//                 Assert.False(result);
//             });
//     }
//
//     [Fact]
//     public void Button_PressedState_DetectedCorrectly()
//     {
//         // Arrange
//         var gui = CreateTestGui();
//         var input = gui.Input;
//         SetupMouseHover(input, new Vector2(50, 25));
//         SetupMouseDown(input, new Vector2(50, 25), MouseButton.Left);
//
//         // Act & Assert
//         SimulateFullControlLifecycle(gui,
//             buildAction: () => gui.Button("Test Button"),
//             renderAction: () =>
//             {
//                 // Button should handle pressed state properly
//                 var result = gui.Button("Test Button");
//                 // Note: pressed is different from clicked
//                 Assert.False(result); // No click event, just held down
//             });
//     }
//
//     [Fact]
//     public void Button_WithNullText_HandlesGracefully()
//     {
//         // Arrange
//         var gui = CreateTestGui();
//
//         // Act & Assert
//         SimulateFullControlLifecycle(gui,
//             buildAction: () => { },
//             renderAction: () =>
//             {
//                 // Should handle null text without throwing
//                 var result = gui.Button(null!);
//                 Assert.False(result);
//             });
//     }
//
//     [Fact]
//     public void Button_MouseHoverThenLeave_HandlesStateTransition()
//     {
//         // Arrange
//         var gui = CreateTestGui();
//         var input = gui.Input;
//
//         // First frame: hover
//         SetupMouseHover(input, new Vector2(50, 25));
//         SimulateFullControlLifecycle(gui,
//             buildAction: () => gui.Button("Hover Test"),
//             renderAction: () =>
//             {
//                 var result = gui.Button("Hover Test");
//                 Assert.False(result);
//             });
//
//         // Reset for second frame: mouse leaves
//         gui.EndFrame();
//         gui.BeginFrame(CreateTestCanvas());
//         ResetInput(input);
//         SetupMouseHover(input, new Vector2(300, 300)); // Far outside
//
//         // Act & Assert
//         SimulateFullControlLifecycle(gui,
//             buildAction: () => gui.Button("Hover Test"),
//             renderAction: () =>
//             {
//                 // Should handle hover state transition without issues
//                 var result = gui.Button("Hover Test");
//                 Assert.False(result);
//             });
//     }
//
//     [Fact]
//     public void Button_RapidClickSequence_HandlesCorrectly()
//     {
//         // Arrange
//         var gui = CreateTestGui();
//         var input = gui.Input;
//         var clickCount = 0;
//
//         // Simulate rapid clicking
//         for (int i = 0; i < 10; i++)
//         {
//             SetupMouseClick(input, new Vector2(50, 25));
//
//             SimulateFullControlLifecycle(gui,
//                 buildAction: () => gui.Button("Rapid Click"),
//                 renderAction: () =>
//                 {
//                     var clicked = gui.Button("Rapid Click");
//                     if (clicked) clickCount++;
//                 });
//
//             // Reset for next frame
//             gui.EndFrame();
//             gui.BeginFrame(CreateTestCanvas());
//             ResetInput(input);
//         }
//
//         // Should register clicks correctly
//         Assert.True(clickCount > 0);
//     }
// }
