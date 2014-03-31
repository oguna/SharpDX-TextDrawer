struct VS_IN
{
	float3 pos : POSITION;
	float4 dif : COLOR;
	float2 tex : TEXCOORD;
};

struct PS_IN
{
	float4 pos : SV_POSITION;
	float4 dif : COLOR;
	float2 tex : TEXCOORD;
};

Texture2D picture;
SamplerState pictureSampler;

PS_IN VS( VS_IN input )
{
	PS_IN output = (PS_IN)0;
	
	output.pos = float4(input.pos, 1.0f);
	output.dif = input.dif;
	output.tex = input.tex;
	
	return output;
}

float4 PS( PS_IN input ) : SV_Target
{
	return picture.Sample(pictureSampler, input.tex) * input.dif;
}

float4 PSUntex( PS_IN input ) : SV_Target
{
	return input.dif;
}