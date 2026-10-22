# 色彩位移

本模组会将大部分场景物体的着色器替换为在色轮上旋转纹理的变体。

如果你想将其作为一种酷炫的 PaletteSwapper 替代方案，只需设置“相位角度”或启用每房间/每区域的相位随机化。请注意，由于相位角度设置单位是度数，将其设为 360 会回到原版效果。

如果你想让所有物体都呈现出宏伟的彩虹色，可以使用 X、Y 和 Z 设置来控制三个轴向的渐变强度。其中 Z 轴的彩虹效果会根据物体在背景中的深度来着色。该效果还可以通过“动画速度”滑块进行动态调整。

## 截图集

![Hornet standing in Bone Bottom, which is repainted yellow](Screenshots/yellow%20bonebottom.png)
![Hornet and Shakra standing in the Deep Docks, which glows green with radiation](Screenshots/radioactive%20docks.png)
![Hornet standing in The Marrow with rainbow sparks flying around her](Screenshots/rainbow%20sparks.png)

# Acknowledgments

Thanks to [BeesNation][] for making the [original HueShifter][orig] for Hollow Knight.

[BeesNation]: https://github.com/beesnation/
[orig]: https://github.com/beesnation/HueShifter
