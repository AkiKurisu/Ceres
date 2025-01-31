using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
namespace Ceres.Editor.Graph
{
    // Modified from https://gitee.com/NKG_admin/NKGMobaBasedOnET/tree/master/Unity/Assets/Model/NKGMOBA/Helper/NodeGraph/Core

    public interface ILayoutNode
    {
        VisualElement View { get; }
        
        IReadOnlyList<ILayoutNode> GetLayoutChildren();
    }
    
    public interface INodeForLayoutConvertor
    {
        float SiblingDistance { get; }
        
        NodeAutoLayoutHelper.TreeNode LayoutRootNode { get; }
        
        NodeAutoLayoutHelper.TreeNode PrimNode2LayoutNode();
        
        void LayoutNode2PrimNode();
    }
    
    public static class NodeAutoLayoutHelper
    {
        [Flags]
        public enum CalculateMode
        {
            // 水平计算模式
            Horizontal = 1,

            // 垂直计算模式
            Vertical = Horizontal << 1,

            // 从上到下，从左到右
            Positive = Horizontal << 2,

            // 从下到上，从右到左
            Negative = Horizontal << 3
        }

        public class TreeNode
        {
            public float w, h; //  Width and height.
            
            public float x, y, prelim, mod, shift, change;
            
            public TreeNode tl, tr; //  Left and right thread.   
            
            public TreeNode el, er; //  Extreme left and right nodes.
            
            public float msel, mser; //  Sum of modifiers at the extreme nodes.
            
            public readonly List<TreeNode> children = new();

            public int ChildrenCount => children.Count; //  Array of children and number of children.
            
            public readonly CalculateMode CalculateMode;

            public TreeNode(float w, float h, float y,
                CalculateMode calculateMode = CalculateMode.Vertical | CalculateMode.Positive)
            {
                this.w = w;
                this.h = h;
                this.y = y;
                CalculateMode = calculateMode;
            }

            public void AddChild(TreeNode child)
            {
                children.Add(child);
            }

            public Vector2 GetPos()
            {
                var calculateResult = new Vector2(x, y);

                // 根节点位于原点，绕原点进行旋转(绕原点的旋转矩阵)，并对节点位置进行修正（因为宽高翻转）
                if (CalculateMode == (CalculateMode.Horizontal | CalculateMode.Negative))
                {
                    Vector2 temp = calculateResult;
                    temp.x = -calculateResult.y - h;
                    temp.y = calculateResult.x;
                    calculateResult = temp;
                }

                if (CalculateMode == (CalculateMode.Horizontal | CalculateMode.Positive))
                {
                    Vector2 temp = calculateResult;
                    temp.x = calculateResult.y;
                    temp.y = -calculateResult.x - w;
                    calculateResult = temp;
                }

                if (CalculateMode == (CalculateMode.Vertical | CalculateMode.Negative))
                {
                    calculateResult.y = -calculateResult.y - h;
                }

                return calculateResult;
            }
        }

        public static void Layout(this INodeForLayoutConvertor nodeForLayoutConvertor)
        {
            if (nodeForLayoutConvertor.PrimNode2LayoutNode() == null)
            {
                return;
            }

            FirstWalk(nodeForLayoutConvertor.LayoutRootNode);
            SecondWalk(nodeForLayoutConvertor.LayoutRootNode, 0);

            nodeForLayoutConvertor.LayoutNode2PrimNode();
        }

        private static void FirstWalk(TreeNode t)
        {
            if (t.ChildrenCount == 0)
            {
                SetExtremes(t);
                return;
            }

            FirstWalk(t.children[0]);
            // Create siblings in contour minimal vertical coordinate and index list.
            IYL ih = UpdateIYL(Bottom(t.children[0].el), 0, null);
            for (int i = 1; i < t.ChildrenCount; i++)
            {
                FirstWalk(t.children[i]);
                // Store lowest vertical coordinate while extreme nodes still point in current subtree.
                float minY = Bottom(t.children[i].er);
                Separate(t, i, ih);
                ih = UpdateIYL(minY, i, ih);
            }

            PositionRoot(t);
            SetExtremes(t);
        }

        private static void SetExtremes(TreeNode t)
        {
            if (t.ChildrenCount == 0)
            {
                t.el = t;
                t.er = t;
                t.msel = t.mser = 0;
            }
            else
            {
                t.el = t.children[0].el;
                t.msel = t.children[0].msel;
                t.er = t.children[t.ChildrenCount - 1].er;
                t.mser = t.children[t.ChildrenCount - 1].mser;
            }
        }

        private static void Separate(TreeNode t, int i, IYL ih)
        {
            //  Right contour node of left siblings and its sum of modifiers.
            TreeNode sr = t.children[i - 1];
            float mssr = sr.mod;
            //  Left contour node of current subtree and its sum of modifiers.
            TreeNode cl = t.children[i];
            float mscl = cl.mod;
            while (sr != null && cl != null)
            {
                if (Bottom(sr) > ih.lowY) ih = ih.nxt;

                //  How far to the left of the right side of sr is the left side of cl?
                float dist = mssr + sr.prelim + sr.w - (mscl + cl.prelim);
                if (dist > 0)
                {
                    mscl += dist;
                    MoveSubtree(t, i, ih.index, dist);
                }

                float sy = Bottom(sr), cy = Bottom(cl);
                //  Advance highest node(s) and sum(s) of modifiers
                if (sy <= cy)
                {
                    sr = NextRightContour(sr);
                    if (sr != null) mssr += sr.mod;
                }

                if (sy >= cy)
                {
                    cl = NextLeftContour(cl);
                    if (cl != null) mscl += cl.mod;
                }
            }

            //  Set threads and update extreme nodes.
            //  In the first case, the current subtree must be taller than the left siblings.
            if (sr == null && cl != null) SetLeftThread(t, i, cl, mscl);
            //  In this case, the left siblings must be taller than the current subtree.
            else if (sr != null) SetRightThread(t, i, sr, mssr);
        }

        private static void MoveSubtree(TreeNode t, int i, int si, float dist)
        {
            //  Move subtree by changing mod.
            t.children[i].mod += dist;
            t.children[i].msel += dist;
            t.children[i].mser += dist;
            DistributeExtra(t, i, si, dist);
        }

        private static TreeNode NextLeftContour(TreeNode t) { return t.ChildrenCount == 0 ? t.tl : t.children[0]; }
        private static TreeNode NextRightContour(TreeNode t) { return t.ChildrenCount == 0 ? t.tr : t.children[t.ChildrenCount - 1]; }
        private static float Bottom(TreeNode t) { return t.y + t.h; }

        private static void SetLeftThread(TreeNode t, int i, TreeNode cl, float modsumcl)
        {
            TreeNode li = t.children[0].el;
            li.tl = cl;
            //  Change mod so that the sum of modifier after following thread is correct.
            float diff = (modsumcl - cl.mod) - t.children[0].msel;
            li.mod += diff;
            //  Change preliminary x coordinate so that the node does not move.
            li.prelim -= diff;
            //  Update extreme node and its sum of modifiers.
            t.children[0].el = t.children[i].el;
            t.children[0].msel = t.children[i].msel;
        }

        //  Symmetrical to setLeftThread.
        private static void SetRightThread(TreeNode t, int i, TreeNode sr, float modsumsr)
        {
            TreeNode ri = t.children[i].er;
            ri.tr = sr;
            float diff = (modsumsr - sr.mod) - t.children[i].mser;
            ri.mod += diff;
            ri.prelim -= diff;
            t.children[i].er = t.children[i - 1].er;
            t.children[i].mser = t.children[i - 1].mser;
        }

        private static void PositionRoot(TreeNode t)
        {
            //  Position root between children, taking into account their mod.
            t.prelim = (t.children[0].prelim + t.children[0].mod + t.children[t.ChildrenCount - 1].mod +
                        t.children[t.ChildrenCount - 1].prelim + t.children[t.ChildrenCount - 1].w) / 2 - t.w / 2;
        }

        private static void SecondWalk(TreeNode t, float modsum)
        {
            modsum += t.mod;
            //  Set absolute (non-relative) horizontal coordinate.
            t.x = t.prelim + modsum;
            AddChildSpacing(t);
            for (int i = 0; i < t.ChildrenCount; i++) SecondWalk(t.children[i], modsum);
        }

        private static void DistributeExtra(TreeNode t, int i, int si, float dist)
        {
            //  Are there intermediate children?
            if (si != i - 1)
            {
                float nr = i - si;
                t.children[si + 1].shift += dist / nr;
                t.children[i].shift -= dist / nr;
                t.children[i].change -= dist - dist / nr;
            }
        }

        //  Process change and shift to add intermediate spacing to mod.
        private static void AddChildSpacing(TreeNode t)
        {
            float d = 0, modsumdelta = 0;
            for (int i = 0; i < t.ChildrenCount; i++)
            {
                d += t.children[i].shift;
                modsumdelta += d + t.children[i].change;
                t.children[i].mod += modsumdelta;
            }
        }

        //  A linked list of the indexes of left siblings and their lowest vertical coordinate.
        private class IYL
        {
            public float lowY;
            
            public int index;
            
            public IYL nxt;

            public IYL(float lowY, int index, IYL nxt)
            {
                this.lowY = lowY;
                this.index = index;
                this.nxt = nxt;
            }
        }

        private static IYL UpdateIYL(float minY, int i, IYL ih)
        {
            //  Remove siblings that are hidden by the new subtree.
            while (ih != null && minY >= ih.lowY) ih = ih.nxt;
            //  Prepend the new subtree.
            return new IYL(minY, i, ih);
        }
    }
}
