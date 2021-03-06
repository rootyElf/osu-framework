﻿// Copyright (c) 2007-2018 ppy Pty Ltd <contact@ppy.sh>.
// Licensed under the MIT Licence - https://raw.githubusercontent.com/ppy/osu-framework/master/LICENCE

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input;
using osu.Framework.Input.Bindings;
using osu.Framework.Testing;
using OpenTK;
using OpenTK.Graphics;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Testing.Input;
using OpenTK.Input;

namespace osu.Framework.Tests.Visual
{
    public class TestCaseKeyBindings : TestCase
    {
        private readonly ManualInputManager manual;
        private readonly KeyBindingTester none, noneExact, unique, all;

        public TestCaseKeyBindings()
        {
            Child = manual = new ManualInputManager
            {
                Child = new GridContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Content = new[]
                    {
                        new Drawable[]
                        {
                            none = new KeyBindingTester(SimultaneousBindingMode.None),
                            noneExact = new KeyBindingTester(SimultaneousBindingMode.NoneExact)
                        },
                        new Drawable[]
                        {
                            unique = new KeyBindingTester(SimultaneousBindingMode.Unique),
                            all = new KeyBindingTester(SimultaneousBindingMode.All)
                        },
                    }
                }
            };

            AddStep("return input", () => manual.UseParentInput = true);
        }

        private readonly List<Key> pressedKeys = new List<Key>();
        private readonly List<MouseButton> pressedMouseButtons = new List<MouseButton>();
        private readonly Dictionary<TestButton, EventCounts> lastEventCounts = new Dictionary<TestButton, EventCounts>();

        private void toggleKey(Key key)
        {
            if (!pressedKeys.Contains(key))
            {
                pressedKeys.Add(key);
                AddStep($"press {key}", () => manual.PressKey(key));
            }
            else
            {
                pressedKeys.Remove(key);
                AddStep($"release {key}", () => manual.ReleaseKey(key));
            }
        }

        private void toggleMouseButton(MouseButton button)
        {
            if (!pressedMouseButtons.Contains(button))
            {
                pressedMouseButtons.Add(button);
                AddStep($"press {button}", () => manual.PressButton(button));
            }
            else
            {
                pressedMouseButtons.Remove(button);
                AddStep($"release {button}", () => manual.ReleaseButton(button));
            }
        }

        private void scrollMouseWheel(int dy)
        {
            AddStep($"scroll wheel {dy}", () => manual.ScrollVerticalBy(dy));
        }

        private void check(TestAction action, params CheckConditions[] entries)
        {
            AddAssert($"check {action}", () =>
            {
                Assert.Multiple(() =>
                {
                    foreach (var entry in entries)
                    {
                        var testButton = entry.Tester[action];

                        if (!lastEventCounts.TryGetValue(testButton, out var count))
                            lastEventCounts[testButton] = count = new EventCounts();

                        count.OnPressedCount += entry.OnPressedDelta;
                        count.OnReleasedCount += entry.OnReleasedDelta;

                        Assert.AreEqual(count.OnPressedCount, testButton.OnPressedCount, $"{testButton.Concurrency} {testButton.Action}");
                        Assert.AreEqual(count.OnReleasedCount, testButton.OnReleasedCount, $"{testButton.Concurrency} {testButton.Action}");
                    }
                });
                return true;
            });
        }

        private void checkPressed(TestAction action, int noneDelta, int noneExactDelta, int uniqueDelta, int allDelta)
        {
            check(action,
                new CheckConditions(none, noneDelta, 0),
                new CheckConditions(noneExact, noneExactDelta, 0),
                new CheckConditions(unique, uniqueDelta, 0),
                new CheckConditions(all, allDelta, 0));
        }

        private void checkReleased(TestAction action, int noneDelta, int noneExactDelta, int uniqueDelta, int allDelta)
        {
            check(action,
                new CheckConditions(none, 0, noneDelta),
                new CheckConditions(noneExact, 0, noneExactDelta),
                new CheckConditions(unique, 0, uniqueDelta),
                new CheckConditions(all, 0, allDelta));
        }

        private void wrapTest(Action inner)
        {
            AddStep("init", () =>
            {
                manual.UseParentInput = false;
                foreach (var mode in new[] { none, noneExact, unique, all })
                {
                    foreach (var action in Enum.GetValues(typeof(TestAction)).Cast<TestAction>())
                    {
                        mode[action].Reset();
                    }
                }

                lastEventCounts.Clear();
            });
            pressedKeys.Clear();
            pressedMouseButtons.Clear();
            inner();
            foreach (var key in pressedKeys.ToArray())
                toggleKey(key);
            foreach (var button in pressedMouseButtons.ToArray())
                toggleMouseButton(button);
            foreach (var mode in new[] { none, noneExact, unique, all })
            {
                foreach (var action in Enum.GetValues(typeof(TestAction)).Cast<TestAction>())
                {
                    var testButton = mode[action];
                    Trace.Assert(testButton.OnPressedCount == testButton.OnReleasedCount);
                }
            }
        }

        [Test]
        public void SimultaneousBindingModes()
        {
            wrapTest(() =>
            {
                toggleKey(Key.A);
                checkPressed(TestAction.A, 1, 1, 1, 1);
                toggleKey(Key.S);
                checkReleased(TestAction.A, 1, 1, 0, 0);
                checkPressed(TestAction.S, 1, 0, 1, 1);
                toggleKey(Key.A);
                checkReleased(TestAction.A, 0, 0, 1, 1);
                checkPressed(TestAction.S, 0, 0, 0, 0);
                toggleKey(Key.S);
                checkReleased(TestAction.S, 1, 0, 1, 1);

                toggleKey(Key.D);
                checkPressed(TestAction.D_or_F, 1, 1, 1, 1);
                toggleKey(Key.F);
                check(TestAction.D_or_F, new CheckConditions(none, 1, 1), new CheckConditions(noneExact, 0, 1), new CheckConditions(unique, 0, 0), new CheckConditions(all, 1, 0));
                toggleKey(Key.F);
                checkReleased(TestAction.D_or_F, 0, 0, 0, 1);
                toggleKey(Key.D);
                checkReleased(TestAction.D_or_F, 1, 0, 1, 1);
            });
        }

        [Test]
        public void ModifierKeys()
        {
            wrapTest(() =>
            {
                toggleKey(Key.ShiftLeft);
                checkPressed(TestAction.Shift, 1, 1, 1, 1);
                toggleKey(Key.A);
                checkReleased(TestAction.Shift, 1, 1, 0, 0);
                checkPressed(TestAction.Shift_A, 1, 1, 1, 1);
                toggleKey(Key.ShiftRight);
                checkPressed(TestAction.Shift, 0, 0, 0, 0);
                checkReleased(TestAction.Shift_A, 0, 0, 0, 0);
                toggleKey(Key.ShiftLeft);
                checkReleased(TestAction.Shift, 0, 0, 0, 0);
                checkReleased(TestAction.Shift_A, 0, 0, 0, 0);
                toggleKey(Key.ShiftRight);
                checkReleased(TestAction.Shift, 0, 0, 1, 1);
                checkReleased(TestAction.Shift_A, 1, 1, 1, 1);
                toggleKey(Key.A);

                toggleKey(Key.ControlLeft);
                toggleKey(Key.ShiftLeft);
                checkPressed(TestAction.Ctrl_and_Shift, 1, 1, 1, 1);
            });
        }

        [Test]
        public void MouseScrollAndButtons()
        {
            var allPressAndReleased = new[]
            {
                new CheckConditions(none, 1, 1),
                new CheckConditions(noneExact, 1, 1),
                new CheckConditions(unique, 1, 1),
                new CheckConditions(all, 1, 1)
            };

            scrollMouseWheel(1);
            check(TestAction.MouseWheelUp, allPressAndReleased);
            scrollMouseWheel(-1);
            check(TestAction.MouseWheelDown, allPressAndReleased);
            toggleMouseButton(MouseButton.Left);
            toggleMouseButton(MouseButton.Left);
            check(TestAction.LeftMouse, allPressAndReleased);
            toggleMouseButton(MouseButton.Right);
            toggleMouseButton(MouseButton.Right);
            check(TestAction.RightMouse, allPressAndReleased);
        }

        private class EventCounts
        {
            public int OnPressedCount;
            public int OnReleasedCount;
        }

        private class CheckConditions
        {
            public readonly KeyBindingTester Tester;
            public readonly int OnPressedDelta;
            public readonly int OnReleasedDelta;

            public CheckConditions(KeyBindingTester tester, int onPressedDelta, int onReleasedDelta)
            {
                Tester = tester;
                OnPressedDelta = onPressedDelta;
                OnReleasedDelta = onReleasedDelta;
            }
        }

        private enum TestAction
        {
            A,
            S,
            D_or_F,
            Ctrl_A,
            Ctrl_S,
            Ctrl_D_or_F,
            Shift_A,
            Shift_S,
            Shift_D_or_F,
            Ctrl_Shift_A,
            Ctrl_Shift_S,
            Ctrl_Shift_D_or_F,
            Ctrl,
            Shift,
            Ctrl_and_Shift,
            Ctrl_or_Shift,
            LeftMouse,
            RightMouse,
            MouseWheelUp,
            MouseWheelDown
        }

        private class TestInputManager : KeyBindingContainer<TestAction>
        {
            public TestInputManager(SimultaneousBindingMode concurrencyMode = SimultaneousBindingMode.None)
                : base(concurrencyMode)
            {
            }

            public override IEnumerable<KeyBinding> DefaultKeyBindings => new[]
            {
                new KeyBinding(InputKey.A, TestAction.A),
                new KeyBinding(InputKey.S, TestAction.S),
                new KeyBinding(InputKey.D, TestAction.D_or_F),
                new KeyBinding(InputKey.F, TestAction.D_or_F),

                new KeyBinding(new[] { InputKey.Control, InputKey.A }, TestAction.Ctrl_A),
                new KeyBinding(new[] { InputKey.Control, InputKey.S }, TestAction.Ctrl_S),
                new KeyBinding(new[] { InputKey.Control, InputKey.D }, TestAction.Ctrl_D_or_F),
                new KeyBinding(new[] { InputKey.Control, InputKey.F }, TestAction.Ctrl_D_or_F),

                new KeyBinding(new[] { InputKey.Shift, InputKey.A }, TestAction.Shift_A),
                new KeyBinding(new[] { InputKey.Shift, InputKey.S }, TestAction.Shift_S),
                new KeyBinding(new[] { InputKey.Shift, InputKey.D }, TestAction.Shift_D_or_F),
                new KeyBinding(new[] { InputKey.Shift, InputKey.F }, TestAction.Shift_D_or_F),

                new KeyBinding(new[] { InputKey.Control, InputKey.Shift, InputKey.A }, TestAction.Ctrl_Shift_A),
                new KeyBinding(new[] { InputKey.Control, InputKey.Shift, InputKey.S }, TestAction.Ctrl_Shift_S),
                new KeyBinding(new[] { InputKey.Control, InputKey.Shift, InputKey.D }, TestAction.Ctrl_Shift_D_or_F),
                new KeyBinding(new[] { InputKey.Control, InputKey.Shift, InputKey.F }, TestAction.Ctrl_Shift_D_or_F),

                new KeyBinding(new[] { InputKey.Control }, TestAction.Ctrl),
                new KeyBinding(new[] { InputKey.Shift }, TestAction.Shift),
                new KeyBinding(new[] { InputKey.Control, InputKey.Shift }, TestAction.Ctrl_and_Shift),
                new KeyBinding(new[] { InputKey.Control }, TestAction.Ctrl_or_Shift),
                new KeyBinding(new[] { InputKey.Shift }, TestAction.Ctrl_or_Shift),

                new KeyBinding(new[] { InputKey.MouseLeft }, TestAction.LeftMouse),
                new KeyBinding(new[] { InputKey.MouseRight }, TestAction.RightMouse),
                new KeyBinding(new[] { InputKey.MouseWheelUp }, TestAction.MouseWheelUp),
                new KeyBinding(new[] { InputKey.MouseWheelDown }, TestAction.MouseWheelDown),
            };

            protected override bool OnKeyDown(InputState state, KeyDownEventArgs args)
            {
                base.OnKeyDown(state, args);
                return false;
            }

            protected override bool OnKeyUp(InputState state, KeyUpEventArgs args)
            {
                base.OnKeyUp(state, args);
                return false;
            }

            protected override bool OnMouseDown(InputState state, MouseDownEventArgs args)
            {
                base.OnMouseDown(state, args);
                return false;
            }

            protected override bool OnMouseUp(InputState state, MouseUpEventArgs args)
            {
                base.OnMouseUp(state, args);
                return false;
            }

            protected override bool OnScroll(InputState state)
            {
                base.OnScroll(state);
                return false;
            }

            public override bool ReceiveMouseInputAt(Vector2 screenSpacePos) => true;
        }

        private class TestButton : Button, IKeyBindingHandler<TestAction>
        {
            public new readonly TestAction Action;
            public readonly SimultaneousBindingMode Concurrency;
            public int OnPressedCount { get; protected set; }
            public int OnReleasedCount { get; protected set; }

            private readonly Box highlight;
            private readonly string actionText;

            public TestButton(TestAction action, SimultaneousBindingMode concurrency)
            {
                Action = action;
                Concurrency = concurrency;

                BackgroundColour = Color4.SkyBlue;
                SpriteText.TextSize *= .8f;
                actionText = action.ToString().Replace('_', ' ');

                RelativeSizeAxes = Axes.X;
                Height = 40;
                Width = 0.3f;
                Padding = new MarginPadding(2);

                Background.Alpha = alphaTarget;

                Add(highlight = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Alpha = 0,
                });
            }

            protected override void Update()
            {
                Text = $"{actionText}: {OnPressedCount}, {OnReleasedCount}";
                base.Update();
            }

            private float alphaTarget = 0.5f;

            public bool OnPressed(TestAction action)
            {
                if (Action == action)
                {
                    if (Concurrency != SimultaneousBindingMode.All)
                        Trace.Assert(OnPressedCount == OnReleasedCount);
                    ++OnPressedCount;

                    alphaTarget += 0.2f;
                    Background.Alpha = alphaTarget;

                    highlight.ClearTransforms();
                    highlight.Alpha = 1f;
                    highlight.FadeOut(200);

                    return true;
                }

                return false;
            }

            public bool OnReleased(TestAction action)
            {
                if (Action == action)
                {
                    ++OnReleasedCount;
                    if (Concurrency != SimultaneousBindingMode.All)
                        Trace.Assert(OnPressedCount == OnReleasedCount);
                    else
                        Trace.Assert(OnReleasedCount <= OnPressedCount);

                    alphaTarget -= 0.2f;
                    Background.Alpha = alphaTarget;

                    return true;
                }

                return false;
            }

            public void Reset()
            {
                OnPressedCount = 0;
                OnReleasedCount = 0;
            }
        }

        private class KeyBindingTester : Container
        {
            private readonly TestButton[] testButtons;

            public KeyBindingTester(SimultaneousBindingMode concurrency)
            {
                RelativeSizeAxes = Axes.Both;

                testButtons = Enum.GetValues(typeof(TestAction)).Cast<TestAction>().Select(t => new TestButton(t, concurrency)).ToArray();

                Children = new Drawable[]
                {
                    new SpriteText
                    {
                        Text = concurrency.ToString(),
                    },
                    new TestInputManager(concurrency)
                    {
                        Y = 30,
                        RelativeSizeAxes = Axes.Both,
                        Child = new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.Both,
                            Children = testButtons
                        }
                    },
                };
            }

            public TestButton this[TestAction action] => testButtons.First(x => x.Action == action);
        }
    }
}
