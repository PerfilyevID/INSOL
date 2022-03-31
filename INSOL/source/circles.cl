__kernel void circleIntersection(
    __global float* circleX, __global float* circleY, __global float* circleR,
    __global float* rayOX, __global float* rayOY, __global float* rayDX, __global float* rayDY,
    __global bool* result)
    {
        int i = get_global_id(0);
        /*
        float2 co = (float2)(circleX[i], circleY[i]);
        float2 p0 = (float2)(rayOX[i], rayOY[i]);
        float2 d0 = (float2)(rayDX[i], rayDY[i]);
        float2 d1 = co - d0;
        float angle = atan2(d1.y, d1.x) - atan2(d0.y, d0.x);
        float distance = length(p0 - co);
        float touch = sin(angle) * distance;
        result[i] = touch < circleR[i];
        */
        result[i] = true;
    }