float ShadowTraceDDA(int layer, vec2 uvStart, vec2 uvEnd)
{
    vec2 dir = uvEnd - uvStart;
    float dirLen = length(dir);

    if (dirLen < EPS || any(lessThan(uvStart, vec2(0.0))) || any(greaterThan(uvStart, vec2(1.0))))
        return 1.0;

    vec2 rd = dir / dirLen;

    vec2 stepA = sign(dir);
    vec2 stepB = step(0.0, stepA);
    int mip = MaxLightMip;
    float t = 0.0;
    float totalWhiteUV = 0.0;
    float inWhite;
    int i;
    for (i = 0; i < MaxTotalSteps; ++i)
    {
        vec2 point = uvStart + rd * t;
        if (t > dirLen || any(lessThan(point, vec2(0.0))) || any(greaterThan(point, vec2(1.0)))) break;
        inWhite = IsWhite(point, layer, mip);
        vec2 mipDimensions = Dimensions(mip);
        vec2 cellSize = 1.0 / mipDimensions;
        ivec2 cell = ivec2(floor(point / cellSize));
        vec2 nextBoundary = (vec2(cell) + stepB) * cellSize;
        vec2 offset = nextBoundary - point;
        vec2 cellLocalUV = fract(point / cellSize);
        float moveX = offset.x / rd.x;
        float moveY = offset.y / rd.y;
        float newT = t;
        if (abs(moveX) < abs(moveY)) {
            newT += moveX + EPS;
        } else {
            newT += moveY + EPS;
        }
        float segDistUV = max(newT - t, EPS);
        t = newT;
        totalWhiteUV += segDistUV * inWhite;
        if (totalWhiteUV >= ShadowSoftness)
            break;
    }
    return 1.0 - clamp(smoothstep(0.0, ShadowSoftness, totalWhiteUV), 0.0, 1.0);
}