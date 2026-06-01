#include "pch-cpp.hpp"





template <typename T1, typename T2, typename T3>
struct VirtualActionInvoker3
{
	typedef void (*Action)(void*, T1, T2, T3, const RuntimeMethod*);

	static inline void Invoke (Il2CppMethodSlot slot, RuntimeObject* obj, T1 p1, T2 p2, T3 p3)
	{
		const VirtualInvokeData& invokeData = il2cpp_codegen_get_virtual_invoke_data(slot, obj);
		((Action)invokeData.methodPtr)(obj, p1, p2, p3, invokeData.method);
	}
};
template <typename R, typename T1>
struct VirtualFuncInvoker1
{
	typedef R (*Func)(void*, T1, const RuntimeMethod*);

	static inline R Invoke (Il2CppMethodSlot slot, RuntimeObject* obj, T1 p1)
	{
		const VirtualInvokeData& invokeData = il2cpp_codegen_get_virtual_invoke_data(slot, obj);
		return ((Func)invokeData.methodPtr)(obj, p1, invokeData.method);
	}
};

struct ComplexTypeSerializer_1_tDF602912B1C8D6B9A968553162AB8E4D9DD4456E;
struct Dictionary_2_tC238B53A07C800163811FB1B9C13665125AC65EC;
struct Dictionary_2_tFC542F52AF721FEF237136A160FE0EBB15A771CA;
struct Dictionary_2_t8BF76F08F2E28AE3B97CD39EBC7A0FE57398B1B0;
struct IFormatter_1_tFA213668D4D2675FDCB90E8B0DFB82B165B2EFB7;
struct ReflectionFormatter_1_t9ECE79657B964479F0BCE360CE09A9FBE9BE265C;
struct Serializer_1_t21A2F748C075C7B3B5ED1023BB5F3EDF2A2D95F5;
struct SerializationCallbackU5BU5D_t0470326C78E5F792A04B36BB2D7CCCEFCD86C6B6;
struct ColorFormatter_tAB92E9F1B455D4671124176CC00A6D7A06B2DEF8;
struct ISerializationPolicy_t0C47BF78FD9B96AE6371585EAE63593988C2FA43;
struct SingleSerializer_t73348AC149C370F26845BA4025414C15BEF0526B;
struct String_t;
struct Type_t;

IL2CPP_EXTERN_C RuntimeClass* ColorFormatter_tAB92E9F1B455D4671124176CC00A6D7A06B2DEF8_il2cpp_TypeInfo_var;
IL2CPP_EXTERN_C RuntimeClass* ComplexTypeSerializer_1_tDF602912B1C8D6B9A968553162AB8E4D9DD4456E_il2cpp_TypeInfo_var;
IL2CPP_EXTERN_C RuntimeClass* ReflectionFormatter_1_t9ECE79657B964479F0BCE360CE09A9FBE9BE265C_il2cpp_TypeInfo_var;
IL2CPP_EXTERN_C RuntimeClass* SingleSerializer_t73348AC149C370F26845BA4025414C15BEF0526B_il2cpp_TypeInfo_var;
IL2CPP_EXTERN_C const RuntimeMethod* ComplexTypeSerializer_1__ctor_m040ED8EA8EA35370B034C4CF32F9171560A9EB92_RuntimeMethod_var;
IL2CPP_EXTERN_C const RuntimeMethod* ReflectionFormatter_1__ctor_m713AB94103AD3CAB1776995EB9180A94D8547D0E_RuntimeMethod_var;


IL2CPP_EXTERN_C_BEGIN
IL2CPP_EXTERN_C_END

#ifdef __clang__
#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Winvalid-offsetof"
#pragma clang diagnostic ignored "-Wunused-variable"
#endif
struct U3CModuleU3E_t173B7B51E1BD4E21DB527508DCEFDA1073158A81 
{
};
struct BaseFormatter_1_t96F6A1D517D09BB127456B54A33FEE14B1A10A60  : public RuntimeObject
{
};
struct MinimalBaseFormatter_1_t985ACE5352709ABF7711304BC558AC5463BED148  : public RuntimeObject
{
};
struct PreventCodeStrippingViaReferences_t73A6D17CC562E67E2D540453D45099F37AB84BCD  : public RuntimeObject
{
};
struct Serializer_t6C6FF0C071058921A25F0197AEBE808891C78EDA  : public RuntimeObject
{
};
struct String_t  : public RuntimeObject
{
	int32_t ____stringLength;
	Il2CppChar ____firstChar;
};
struct ValueType_t6D9B272BD21782F0A9A14F2E41F85A50E97A986F  : public RuntimeObject
{
};
struct ValueType_t6D9B272BD21782F0A9A14F2E41F85A50E97A986F_marshaled_pinvoke
{
};
struct ValueType_t6D9B272BD21782F0A9A14F2E41F85A50E97A986F_marshaled_com
{
};
struct ReflectionFormatter_1_t9ECE79657B964479F0BCE360CE09A9FBE9BE265C  : public BaseFormatter_1_t96F6A1D517D09BB127456B54A33FEE14B1A10A60
{
	RuntimeObject* ___U3COverridePolicyU3Ek__BackingField;
};
struct Serializer_1_tA0FADDB9205C5476215FDFB4CDF31D8F268D85ED  : public Serializer_t6C6FF0C071058921A25F0197AEBE808891C78EDA
{
};
struct Serializer_1_t21A2F748C075C7B3B5ED1023BB5F3EDF2A2D95F5  : public Serializer_t6C6FF0C071058921A25F0197AEBE808891C78EDA
{
};
struct Boolean_t09A6377A54BE2F9E6985A8149F19234FD7DDFE22 
{
	bool ___m_value;
};
struct Color_tD001788D726C3A7F1379BEED0260B9591F440C1F 
{
	float ___r;
	float ___g;
	float ___b;
	float ___a;
};
struct ColorFormatter_tAB92E9F1B455D4671124176CC00A6D7A06B2DEF8  : public MinimalBaseFormatter_1_t985ACE5352709ABF7711304BC558AC5463BED148
{
};
struct Single_t4530F2FF86FCB0DC29F35385CA1BD21BE294761C 
{
	float ___m_value;
};
struct Void_t4861ACF8F4594C3437BB48B6E56783494B843915 
{
	union
	{
		struct
		{
		};
		uint8_t Void_t4861ACF8F4594C3437BB48B6E56783494B843915__padding[1];
	};
};
struct ComplexTypeSerializer_1_tDF602912B1C8D6B9A968553162AB8E4D9DD4456E  : public Serializer_1_tA0FADDB9205C5476215FDFB4CDF31D8F268D85ED
{
};
struct SingleSerializer_t73348AC149C370F26845BA4025414C15BEF0526B  : public Serializer_1_t21A2F748C075C7B3B5ED1023BB5F3EDF2A2D95F5
{
};
struct String_t_StaticFields
{
	String_t* ___Empty;
};
struct Boolean_t09A6377A54BE2F9E6985A8149F19234FD7DDFE22_StaticFields
{
	String_t* ___TrueString;
	String_t* ___FalseString;
};
struct ColorFormatter_tAB92E9F1B455D4671124176CC00A6D7A06B2DEF8_StaticFields
{
	Serializer_1_t21A2F748C075C7B3B5ED1023BB5F3EDF2A2D95F5* ___FloatSerializer;
};
struct ComplexTypeSerializer_1_tDF602912B1C8D6B9A968553162AB8E4D9DD4456E_StaticFields
{
	bool ___ComplexTypeMayBeBoxedValueType;
	bool ___ComplexTypeIsAbstract;
	bool ___ComplexTypeIsNullable;
	bool ___ComplexTypeIsValueType;
	Type_t* ___TypeOf_T;
	bool ___AllowDeserializeInvalidDataForT;
	Dictionary_2_tC238B53A07C800163811FB1B9C13665125AC65EC* ___FormattersByPolicy;
	RuntimeObject* ___FormattersByPolicy_LOCK;
	RuntimeObject* ___UnityPolicy;
	RuntimeObject* ___StrictPolicy;
	RuntimeObject* ___EverythingPolicy;
	RuntimeObject* ___UnityPolicyFormatter;
	RuntimeObject* ___StrictPolicyFormatter;
	RuntimeObject* ___EverythingPolicyFormatter;
};
#ifdef __clang__
#pragma clang diagnostic pop
#endif


IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR void ReflectionFormatter_1__ctor_m713AB94103AD3CAB1776995EB9180A94D8547D0E_gshared (ReflectionFormatter_1_t9ECE79657B964479F0BCE360CE09A9FBE9BE265C* __this, const RuntimeMethod* method) ;
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR void ComplexTypeSerializer_1__ctor_m040ED8EA8EA35370B034C4CF32F9171560A9EB92_gshared (ComplexTypeSerializer_1_tDF602912B1C8D6B9A968553162AB8E4D9DD4456E* __this, const RuntimeMethod* method) ;

IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR void SingleSerializer__ctor_m62CF53A863CFAA4F170111C220B4F2356ACB1BD9 (SingleSerializer_t73348AC149C370F26845BA4025414C15BEF0526B* __this, const RuntimeMethod* method) ;
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR void ColorFormatter__ctor_m2614D1810AE63660388E86CA73EF48DF1CBF7888 (ColorFormatter_tAB92E9F1B455D4671124176CC00A6D7A06B2DEF8* __this, const RuntimeMethod* method) ;
inline void ReflectionFormatter_1__ctor_m713AB94103AD3CAB1776995EB9180A94D8547D0E (ReflectionFormatter_1_t9ECE79657B964479F0BCE360CE09A9FBE9BE265C* __this, const RuntimeMethod* method)
{
	((  void (*) (ReflectionFormatter_1_t9ECE79657B964479F0BCE360CE09A9FBE9BE265C*, const RuntimeMethod*))ReflectionFormatter_1__ctor_m713AB94103AD3CAB1776995EB9180A94D8547D0E_gshared)(__this, method);
}
inline void ComplexTypeSerializer_1__ctor_m040ED8EA8EA35370B034C4CF32F9171560A9EB92 (ComplexTypeSerializer_1_tDF602912B1C8D6B9A968553162AB8E4D9DD4456E* __this, const RuntimeMethod* method)
{
	((  void (*) (ComplexTypeSerializer_1_tDF602912B1C8D6B9A968553162AB8E4D9DD4456E*, const RuntimeMethod*))ComplexTypeSerializer_1__ctor_m040ED8EA8EA35370B034C4CF32F9171560A9EB92_gshared)(__this, method);
}
#ifdef __clang__
#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Winvalid-offsetof"
#pragma clang diagnostic ignored "-Wunused-variable"
#endif
#ifdef __clang__
#pragma clang diagnostic pop
#endif
#ifdef __clang__
#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Winvalid-offsetof"
#pragma clang diagnostic ignored "-Wunused-variable"
#endif
// Method Definition Index: 126823
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR void PreventCodeStrippingViaReferences__cctor_m2299EBFF5769500E8639CDB151322A7B2C673EB6 (const RuntimeMethod* method) 
{
	static bool s_Il2CppMethodInitialized;
	if (!s_Il2CppMethodInitialized)
	{
		il2cpp_codegen_initialize_runtime_metadata((uintptr_t*)&ColorFormatter_tAB92E9F1B455D4671124176CC00A6D7A06B2DEF8_il2cpp_TypeInfo_var);
		il2cpp_codegen_initialize_runtime_metadata((uintptr_t*)&ComplexTypeSerializer_1__ctor_m040ED8EA8EA35370B034C4CF32F9171560A9EB92_RuntimeMethod_var);
		il2cpp_codegen_initialize_runtime_metadata((uintptr_t*)&ComplexTypeSerializer_1_tDF602912B1C8D6B9A968553162AB8E4D9DD4456E_il2cpp_TypeInfo_var);
		il2cpp_codegen_initialize_runtime_metadata((uintptr_t*)&ReflectionFormatter_1__ctor_m713AB94103AD3CAB1776995EB9180A94D8547D0E_RuntimeMethod_var);
		il2cpp_codegen_initialize_runtime_metadata((uintptr_t*)&ReflectionFormatter_1_t9ECE79657B964479F0BCE360CE09A9FBE9BE265C_il2cpp_TypeInfo_var);
		il2cpp_codegen_initialize_runtime_metadata((uintptr_t*)&SingleSerializer_t73348AC149C370F26845BA4025414C15BEF0526B_il2cpp_TypeInfo_var);
		s_Il2CppMethodInitialized = true;
	}
	bool V_0 = false;
	float V_1 = 0.0f;
	Color_tD001788D726C3A7F1379BEED0260B9591F440C1F V_2;
	memset((&V_2), 0, sizeof(V_2));
	SingleSerializer_t73348AC149C370F26845BA4025414C15BEF0526B* G_B2_0 = NULL;
	SingleSerializer_t73348AC149C370F26845BA4025414C15BEF0526B* G_B1_0 = NULL;
	ComplexTypeSerializer_1_tDF602912B1C8D6B9A968553162AB8E4D9DD4456E* G_B4_0 = NULL;
	ComplexTypeSerializer_1_tDF602912B1C8D6B9A968553162AB8E4D9DD4456E* G_B3_0 = NULL;
	{
		V_0 = (bool)0;
		il2cpp_codegen_initobj((&V_1), sizeof(float));
		SingleSerializer_t73348AC149C370F26845BA4025414C15BEF0526B* L_0 = (SingleSerializer_t73348AC149C370F26845BA4025414C15BEF0526B*)il2cpp_codegen_object_new(SingleSerializer_t73348AC149C370F26845BA4025414C15BEF0526B_il2cpp_TypeInfo_var);
		SingleSerializer__ctor_m62CF53A863CFAA4F170111C220B4F2356ACB1BD9(L_0, NULL);
		bool L_1 = V_0;
		if (!L_1)
		{
			G_B2_0 = L_0;
			goto IL_0026;
		}
		G_B1_0 = L_0;
	}
	{
		SingleSerializer_t73348AC149C370F26845BA4025414C15BEF0526B* L_2 = G_B1_0;
		NullCheck(L_2);
		RuntimeObject* L_3;
		L_3 = VirtualFuncInvoker1< RuntimeObject*, RuntimeObject* >::Invoke(4, L_2, (RuntimeObject*)NULL);
		SingleSerializer_t73348AC149C370F26845BA4025414C15BEF0526B* L_4 = L_2;
		NullCheck(L_4);
		VirtualActionInvoker3< String_t*, RuntimeObject*, RuntimeObject* >::Invoke(5, L_4, (String_t*)NULL, NULL, (RuntimeObject*)NULL);
		G_B2_0 = L_4;
	}

IL_0026:
	{
		il2cpp_codegen_initobj((&V_2), sizeof(Color_tD001788D726C3A7F1379BEED0260B9591F440C1F));
		ColorFormatter_tAB92E9F1B455D4671124176CC00A6D7A06B2DEF8* L_5 = (ColorFormatter_tAB92E9F1B455D4671124176CC00A6D7A06B2DEF8*)il2cpp_codegen_object_new(ColorFormatter_tAB92E9F1B455D4671124176CC00A6D7A06B2DEF8_il2cpp_TypeInfo_var);
		ColorFormatter__ctor_m2614D1810AE63660388E86CA73EF48DF1CBF7888(L_5, NULL);
		ReflectionFormatter_1_t9ECE79657B964479F0BCE360CE09A9FBE9BE265C* L_6 = (ReflectionFormatter_1_t9ECE79657B964479F0BCE360CE09A9FBE9BE265C*)il2cpp_codegen_object_new(ReflectionFormatter_1_t9ECE79657B964479F0BCE360CE09A9FBE9BE265C_il2cpp_TypeInfo_var);
		ReflectionFormatter_1__ctor_m713AB94103AD3CAB1776995EB9180A94D8547D0E(L_6, ReflectionFormatter_1__ctor_m713AB94103AD3CAB1776995EB9180A94D8547D0E_RuntimeMethod_var);
		ComplexTypeSerializer_1_tDF602912B1C8D6B9A968553162AB8E4D9DD4456E* L_7 = (ComplexTypeSerializer_1_tDF602912B1C8D6B9A968553162AB8E4D9DD4456E*)il2cpp_codegen_object_new(ComplexTypeSerializer_1_tDF602912B1C8D6B9A968553162AB8E4D9DD4456E_il2cpp_TypeInfo_var);
		ComplexTypeSerializer_1__ctor_m040ED8EA8EA35370B034C4CF32F9171560A9EB92(L_7, ComplexTypeSerializer_1__ctor_m040ED8EA8EA35370B034C4CF32F9171560A9EB92_RuntimeMethod_var);
		bool L_8 = V_0;
		if (!L_8)
		{
			G_B4_0 = L_7;
			goto IL_0057;
		}
		G_B3_0 = L_7;
	}
	{
		ComplexTypeSerializer_1_tDF602912B1C8D6B9A968553162AB8E4D9DD4456E* L_9 = G_B3_0;
		NullCheck(L_9);
		RuntimeObject* L_10;
		L_10 = VirtualFuncInvoker1< RuntimeObject*, RuntimeObject* >::Invoke(4, L_9, (RuntimeObject*)NULL);
		ComplexTypeSerializer_1_tDF602912B1C8D6B9A968553162AB8E4D9DD4456E* L_11 = L_9;
		NullCheck(L_11);
		VirtualActionInvoker3< String_t*, RuntimeObject*, RuntimeObject* >::Invoke(5, L_11, (String_t*)NULL, NULL, (RuntimeObject*)NULL);
		G_B4_0 = L_11;
	}

IL_0057:
	{
		return;
	}
}
#ifdef __clang__
#pragma clang diagnostic pop
#endif
